using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.ApiContracts;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Despacho;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.RpaFleet.Auth;
using Viariato.Modules.RpaFleet.Domain;
using Viariato.Modules.RpaFleet.Persistence;
using Viariato.Modules.RpaFleet.Lanzador;
using RpaLanzadorOptions = Viariato.Modules.RpaFleet.Lanzador.LanzadorOptions;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>
/// What a launcher calls: the process that keeps a robot running only while it has work, instead of leaving it switched on.
/// It asks with the robot's own key (it holds it anyway, to hand it to the robot it starts) plus the
/// <see cref="RpaHeaders.Lanzador"/> header, which keeps these calls from counting as the robot being connected.
///
/// The launcher decides only <b>when a robot is running</b>. Which step goes to which copy stays where it was: the robot it
/// starts asks <c>POST /rpa/cola/siguiente</c> like any other.
/// </summary>
internal static class LanzadorEndpoints
{
    public const int LongitudMaximaDelDetalle = 500;

    public static void MapLanzadorEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/rpa/lanzador")
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(ApiKeyDefaults.AuthenticationScheme)
                .RequireClaim(ApiKeyDefaults.DespliegueIdClaimType));

        group.MapGet("/trabajo", GetTrabajoAsync);
        group.MapPut("/estado", PutEstadoAsync);

        // The launcher that runs next to Viriato: no robot key, the secret both share (see SecretoDelLanzador).
        var delLanzador = endpoints.MapGroup("/api/v1/rpa/lanzador")
            .AllowAnonymous()
            .AddEndpointFilter(async (contexto, siguiente) =>
            {
                var secreto = contexto.HttpContext.RequestServices.GetRequiredService<SecretoDelLanzador>();
                if (!secreto.Disponible) return Results.NotFound();
                return secreto.Coincide(contexto.HttpContext.Request.Headers[RpaHeaders.SecretoDelLanzador].ToString())
                    ? await siguiente(contexto)
                    : Results.Unauthorized();
            });

        delLanzador.MapGet("/catalogo", GetCatalogoAsync);
        delLanzador.MapGet("/versiones/{id:guid}/paquete", GetPaqueteAsync);
    }

    /// <summary>
    /// Which robots the launcher next to Viriato runs: every Despliegue it is told to run whose service has a robot version uploaded
    /// (on an active machine, of an active service), with the latest version or the one it is held at, how to run it, and a fresh
    /// pass for it. Read every round.
    /// </summary>
    private static async Task<IResult> GetCatalogoAsync(
        AppDbContext db, SecretoDelLanzador secreto, IOptions<RpaLanzadorOptions> opciones, CancellationToken ct)
    {
        var o = opciones.Value;
        var ajustes = await db.Set<AjustesDelLanzador>().AsNoTracking().FirstOrDefaultAsync(ct) ?? new AjustesDelLanzador();
        var despliegues = (await db.Set<Despliegue>().AsNoTracking().ConSuRobot()
                .Where(d => d.EnElLanzador && d.Servicio!.Activo && d.Equipo!.Activo)
                .OrderBy(d => d.CreatedAt)
                .ToListAsync(ct))
            .Where(d => d.VersionQueCorre is not null)
            .ToList();

        var flujoIds = despliegues.Select(d => d.FlujoId).Distinct().ToList();
        var flujos = await db.Set<Flujo>().AsNoTracking().Where(f => flujoIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Nombre, ct);
        var vecesPorServicio = despliegues.GroupBy(d => d.ServicioId).ToDictionary(g => g.Key, g => g.Count());
        var vista = string.IsNullOrWhiteSpace(o.UrlPublica) ? null : $"{o.UrlPublica.TrimEnd('/')}/vista/vnc.html?path=vista/websockify";

        var robots = despliegues.Select(d =>
        {
            var v = d.VersionQueCorre!;
            var nombre = vecesPorServicio[d.ServicioId] > 1
                ? $"{d.Servicio!.Nombre} · {flujos.GetValueOrDefault(d.FlujoId, d.FlujoId.ToString("N")[..8])}"
                : d.Servicio!.Nombre;

            var entorno = v.EntornoJson is null
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(v.EntornoJson) ?? [];
            // Where people see its screen from: through Viriato's own address, /vista/.
            if (v.Pantalla && vista is not null) entorno.TryAdd("Vista__UrlPublica", vista);

            return new RobotDelCatalogoDto(
                d.Id, nombre, secreto.EmitirPase(d.Id), v.Id, v.Numero, v.Sha256, v.Raiz, v.Comando, v.Argumentos, v.Pantalla,
                d.MemoriaMaximaMb ?? v.MemoriaMaximaMb, d.Replicas, entorno);
        }).ToList();

        // Still two with the same name (same service and process on two machines): the machine tells them apart.
        var repetidos = robots.GroupBy(r => r.Nombre, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        robots = robots.Select(r => repetidos.Contains(r.Nombre)
            ? r with { Nombre = $"{r.Nombre} ({despliegues.First(d => d.Id == r.DespliegueId).Equipo!.Nombre})" }
            : r).ToList();

        return Results.Ok(new CatalogoDelLanzadorDto(Math.Max(1, ajustes.MaxRobotsALaVez), Math.Max(0, ajustes.MaxConNavegador), robots));
    }

    /// <summary>The zip of a robot version, for the launcher to unpack and run.</summary>
    private static async Task<IResult> GetPaqueteAsync(Guid id, AppDbContext db, AlmacenDeRobots almacen, HttpContext http, CancellationToken ct)
    {
        var version = await db.Set<VersionDeRobot>().AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);
        if (version is null) return ProblemResults.NotFound(http, "Versión no encontrada.");

        try
        {
            return Results.File(almacen.Abrir(version.Ruta), "application/zip", version.NombreArchivo);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return ProblemResults.NotFound(http, "El archivo de esa versión ya no está en el servidor: súbela de nuevo.");
        }
    }

    /// <summary>Whether it is worth having a copy of this robot running: what waits in its queue and what its copies hold.
    /// Read-only, and counted exactly as the dispatcher counts it.</summary>
    private static async Task<IResult> GetTrabajoAsync(ClaimsPrincipal principal, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var despliegue = await db.Set<Despliegue>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == principal.GetDespliegueId(), ct);
        if (despliegue is null) return ProblemResults.NotFound(http, "Despliegue no encontrado.");

        var pendientes = await (
            from detalle in db.Set<RpaEjecucionDetalle>().AsNoTracking()
            join paso in db.Set<EjecucionPaso>().AsNoTracking() on detalle.EjecucionPasoId equals paso.Id
            join pasoDef in db.Set<FlujoPasoDef>().AsNoTracking() on paso.FlujoPasoDefId equals pasoDef.Id
            join version in db.Set<FlujoVersion>().AsNoTracking() on pasoDef.FlujoVersionId equals version.Id
            where detalle.DespliegueId == null && paso.Estado == EjecucionPasoEstado.EnProgreso
                && pasoDef.ServicioId == despliegue.ServicioId && version.FlujoId == despliegue.FlujoId
            select detalle.Id
        ).CountAsync(ct);

        var ahora = DateTimeOffset.UtcNow;
        var enMarcha = (await PasoEnMarcha.CargarAsync(db, equipoId: null, ct)).Where(p => !p.Vencido(ahora)).ToList();
        var alLimite = (await DespachoEquipo.ServiciosAlLimiteGlobalAsync(db, enMarcha, ct)).Contains(despliegue.ServicioId);

        return Results.Ok(new TrabajoDelDespliegueDto(
            despliegue.Encendido, pendientes, enMarcha.Count(p => p.DespliegueId == despliegue.Id), alLimite));
    }

    /// <summary>The launcher's word on this robot — started, waiting and why, failed and how — kept on the Despliegue for
    /// the machine's screen. Only the latest matters, so it overwrites.</summary>
    private static async Task<IResult> PutEstadoAsync(
        EstadoDelLanzadorRequest request, ClaimsPrincipal principal, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>();
        if (!EstadosDelLanzador.Todos.Contains(request.Estado))
        {
            errores["estado"] = [$"Debe ser uno de: {string.Join(", ", EstadosDelLanzador.Todos)}."];
        }

        if (request.Copias < 0) errores["copias"] = ["No puede ser negativo."];
        if (errores.Count > 0) return Results.ValidationProblem(errores);

        var detalle = string.IsNullOrWhiteSpace(request.Detalle) ? null : request.Detalle.Trim();
        if (detalle is { Length: > LongitudMaximaDelDetalle }) detalle = detalle[..LongitudMaximaDelDetalle];

        var ahora = DateTimeOffset.UtcNow;
        var filas = await db.Set<Despliegue>()
            .Where(d => d.Id == principal.GetDespliegueId())
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LanzadorEstado, request.Estado)
                .SetProperty(d => d.LanzadorCopias, request.Copias)
                .SetProperty(d => d.LanzadorDetalle, detalle)
                .SetProperty(d => d.LanzadorVistoEn, ahora), ct);

        return filas == 0 ? ProblemResults.NotFound(http, "Despliegue no encontrado.") : Results.NoContent();
    }
}
