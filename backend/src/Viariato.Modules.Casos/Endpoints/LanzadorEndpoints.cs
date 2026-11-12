using System.Security.Claims;
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
        if (request.MaxCopias < 0) errores["maxCopias"] = ["No puede ser negativo."];
        if (errores.Count > 0) return Results.ValidationProblem(errores);

        var detalle = string.IsNullOrWhiteSpace(request.Detalle) ? null : request.Detalle.Trim();
        if (detalle is { Length: > LongitudMaximaDelDetalle }) detalle = detalle[..LongitudMaximaDelDetalle];

        var ahora = DateTimeOffset.UtcNow;
        var filas = await db.Set<Despliegue>()
            .Where(d => d.Id == principal.GetDespliegueId())
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.LanzadorEstado, request.Estado)
                .SetProperty(d => d.LanzadorCopias, request.Copias)
                .SetProperty(d => d.LanzadorMaxCopias, request.MaxCopias)
                .SetProperty(d => d.LanzadorDetalle, detalle)
                .SetProperty(d => d.LanzadorVistoEn, ahora), ct);

        return filas == 0 ? ProblemResults.NotFound(http, "Despliegue no encontrado.") : Results.NoContent();
    }
}
