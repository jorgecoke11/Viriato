using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>What a screen polls to know whether what it shows is still true: a short signature that changes whenever something it
/// shows could have. The screen asks for it often and only reloads its real (heavier) data when it changes, so a screen left open on
/// a case nobody is touching costs one tiny query every few seconds instead of the whole history.</summary>
public sealed record PulsoDto(string Firma);

internal static class PulsoEndpoints
{
    public static void MapPulsoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var read = endpoints.MapGroup("/api/v1/casos").RequireAuthorization(Permissions.CasosRead);
        read.MapGet("/resumen/pulso", GetPulsoDelPanelAsync);
        read.MapGet("/{id:guid}/pulso", GetPulsoDelCasoAsync);
    }

    /// <summary>The dashboard's: every case of the caller's processes moves <c>UpdatedAt</c> when its estado does, and a case that is
    /// created or removed changes the count. One aggregate over an indexed column, whatever the window the panel shows.</summary>
    private static async Task<IResult> GetPulsoDelPanelAsync(AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var flujos = await FlujoAccessAuthorization.FlujosAsignadosAsync(db, http.User.GetUserId(), ct);
        if (flujos.Count == 0) return Results.Ok(new PulsoDto("0"));

        var agregado = await db.Set<Caso>().AsNoTracking()
            .Where(c => flujos.Contains(c.FlujoId))
            .GroupBy(_ => 1)
            .Select(g => new { Total = g.Count(), Ultimo = g.Max(c => c.UpdatedAt) })
            .FirstOrDefaultAsync(ct);

        // The processes themselves are part of it: being given or losing one changes the cards.
        var procesos = string.Join(',', flujos.Order());
        return Results.Ok(new PulsoDto($"{procesos.GetHashCode():x}|{agregado?.Total ?? 0}|{agregado?.Ultimo.UtcTicks ?? 0}"));
    }

    /// <summary>A case's: everything its screen shows — its estados, its history (append-only, so the count says it), its evidence and
    /// documents, its steps and how far the robot says it is on one. A single query of indexed subqueries.</summary>
    private static async Task<IResult> GetPulsoDelCasoAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var pulso = await db.Set<Caso>().AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.FlujoId,
                c.UpdatedAt,
                Eventos = db.Set<CasoEvento>().Count(e => e.CasoId == id),
                Evidencias = db.Set<Evidencia>().Count(e => e.CasoId == id),
                Documentos = db.Set<Documento>().Count(d => d.CasoId == id),
                Pasos = db.Set<EjecucionPaso>().Count(p => p.CasoId == id),
                Progreso = db.Set<RpaEjecucionDetalle>().Where(d => d.EjecucionPaso!.CasoId == id).Max(d => d.ProgresoAt),
            })
            .FirstOrDefaultAsync(ct);
        if (pulso is null) return ProblemResults.NotFound(http, "Caso no encontrado.");
        if (!await FlujoAccessAuthorization.TieneAccesoAlFlujoAsync(db, http.User.GetUserId(), pulso.FlujoId, ct))
            return ProblemResults.NotFound(http, "Caso no encontrado.");

        return Results.Ok(new PulsoDto(
            $"{pulso.UpdatedAt.UtcTicks}|{pulso.Eventos}|{pulso.Evidencias}|{pulso.Documentos}|{pulso.Pasos}|{pulso.Progreso?.UtcTicks ?? 0}"));
    }
}
