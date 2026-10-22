using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Flujos.Contracts;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Flujos.Esquemas;
using Viariato.Modules.Flujos.Validation;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Flujos.Endpoints;

/// <summary>
/// The configuration side of launchers: whoever manages the creators of a Flujo decides here when each one fires by itself.
/// Firing them is in Casos (it needs the orchestrator). Like a creator, a launcher can be deleted: the Casos it opened do not point at it.
/// </summary>
internal static class LanzadoresDeCasoEndpoints
{
    public static void MapLanzadoresDeCasoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/flujos/{flujoId:guid}/lanzadores", ListAsync).RequireAuthorization(Permissions.FlujosRead);

        var manage = endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/lanzadores").RequireAuthorization(Permissions.FlujosCreadores);
        manage.MapPost("/", CreateAsync);
        manage.MapPut("/{id:guid}", ReplaceAsync);
        manage.MapDelete("/{id:guid}", DeleteAsync);
    }

    private static async Task<IResult> ListAsync(Guid flujoId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (!await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct)) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        var lanzadores = await db.Set<LanzadorDeCaso>().AsNoTracking()
            .Include(l => l.CreadorDeCaso)
            .Where(l => l.FlujoId == flujoId)
            .OrderBy(l => l.Nombre)
            .ToListAsync(ct);

        var ahora = DateTimeOffset.UtcNow;
        return Results.Ok(lanzadores.Select(l => l.ToDto(ahora)).ToList());
    }

    private static async Task<IResult> CreateAsync(
        Guid flujoId, GuardarLanzadorDeCasoRequest request, GuardarLanzadorDeCasoRequestValidator validator,
        AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        if (!await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct)) return ProblemResults.NotFound(http, "Flujo no encontrado.");
        if (await ComprobarCreadorAsync(db, http, flujoId, request, ct) is { } rechazo) return rechazo;
        if (await db.Set<LanzadorDeCaso>().AnyAsync(l => l.FlujoId == flujoId && l.Nombre == request.Nombre, ct))
        {
            return ProblemResults.Conflict(http, "Ya existe un lanzador con ese nombre en este proceso.");
        }

        var now = DateTimeOffset.UtcNow;
        var lanzador = new LanzadorDeCaso { FlujoId = flujoId, Nombre = request.Nombre, Cron = request.Cron, ZonaHoraria = request.ZonaHoraria, CreatedAt = now, UpdatedAt = now };
        Aplicar(lanzador, request);
        // The moments of today that are already past are not fired when it is created: it starts from the next one.
        lanzador.UltimoDisparoEn = now;

        db.Add(lanzador);
        await db.SaveChangesAsync(ct);

        return Results.Ok(await CargarAsync(db, lanzador.Id, ct));
    }

    private static async Task<IResult> ReplaceAsync(
        Guid flujoId, Guid id, GuardarLanzadorDeCasoRequest request, GuardarLanzadorDeCasoRequestValidator validator,
        AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var lanzador = await db.Set<LanzadorDeCaso>().FirstOrDefaultAsync(l => l.Id == id && l.FlujoId == flujoId, ct);
        if (lanzador is null) return ProblemResults.NotFound(http, "Lanzador no encontrado.");

        if (await ComprobarCreadorAsync(db, http, flujoId, request, ct) is { } rechazo) return rechazo;
        if (await db.Set<LanzadorDeCaso>().AnyAsync(l => l.FlujoId == flujoId && l.Nombre == request.Nombre && l.Id != id, ct))
        {
            return ProblemResults.Conflict(http, "Ya existe un lanzador con ese nombre en este proceso.");
        }

        var reactivado = request.Activo && !lanzador.Activo;
        Aplicar(lanzador, request);
        // Waking a paused launcher does not fire what went by while it slept.
        if (reactivado) lanzador.UltimoDisparoEn = DateTimeOffset.UtcNow;
        lanzador.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(await CargarAsync(db, lanzador.Id, ct));
    }

    private static async Task<IResult> DeleteAsync(Guid flujoId, Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var lanzador = await db.Set<LanzadorDeCaso>().FirstOrDefaultAsync(l => l.Id == id && l.FlujoId == flujoId, ct);
        if (lanzador is null) return ProblemResults.NotFound(http, "Lanzador no encontrado.");

        db.Remove(lanzador);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static void Aplicar(LanzadorDeCaso lanzador, GuardarLanzadorDeCasoRequest request)
    {
        lanzador.Nombre = request.Nombre.Trim();
        lanzador.Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
        lanzador.CreadorDeCasoId = request.CreadorDeCasoId;
        lanzador.DatosJson = string.IsNullOrWhiteSpace(request.DatosJson) ? null : request.DatosJson.Trim();
        lanzador.Cron = string.Join("\n", request.Cron.Split(['\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        lanzador.ZonaHoraria = request.ZonaHoraria.Trim();
        lanzador.ToleranciaMinutos = request.ToleranciaMinutos;
        lanzador.Activo = request.Activo;
    }

    /// <summary>The creator has to belong to this Flujo, and each case's data — the creator's defaults with the launcher's laid over them —
    /// have to fit its form: otherwise the launcher would only fail later, at a time of day when nobody is looking.</summary>
    private static async Task<IResult?> ComprobarCreadorAsync(
        AppDbContext db, HttpContext http, Guid flujoId, GuardarLanzadorDeCasoRequest request, CancellationToken ct)
    {
        var creador = await db.Set<CreadorDeCaso>().AsNoTracking().Include(c => c.Formulario)
            .FirstOrDefaultAsync(c => c.Id == request.CreadorDeCasoId && c.FlujoId == flujoId, ct);
        if (creador is null) return ProblemResults.Conflict(http, "El creador indicado no existe en este proceso.");

        if (creador.Formulario is { } formulario)
        {
            DatosDeLanzador.TryElementos(string.IsNullOrWhiteSpace(request.DatosJson) ? null : request.DatosJson, out var elementos, out _);
            for (var i = 0; i < elementos.Count; i++)
            {
                var errores = EsquemaDatos.ValidarDatos(formulario.EsquemaJson, MezclaDeDatos.Mezclar(creador.DatosPorDefectoJson, elementos[i]));
                if (errores.Count == 0) continue;
                var cual = elementos.Count > 1 ? $" (elemento {i + 1} de la lista)" : string.Empty;
                return ProblemResults.Conflict(http, $"Los datos no encajan con el formulario «{formulario.Nombre}»{cual}: {string.Join(" ", errores)}");
            }
        }

        return null;
    }

    private static async Task<LanzadorDeCasoDto> CargarAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        (await db.Set<LanzadorDeCaso>().AsNoTracking().Include(l => l.CreadorDeCaso).FirstAsync(l => l.Id == id, ct)).ToDto(DateTimeOffset.UtcNow);
}
