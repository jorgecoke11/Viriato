using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>
/// A Despliegue proves ownership of one unit of work, not blanket access to a Flujo — narrower than
/// (and unrelated to) the AsignacionFlujo boundary user-facing endpoints enforce. It can only act on
/// an EjecucionPaso it actually claimed via POST /api/v1/rpa/cola/siguiente.
/// </summary>
internal static class RpaWorkerAuthorization
{
    public static async Task<RpaEjecucionDetalle?> RequireClaimedStepAsync(AppDbContext db, Guid despliegueId, Guid ejecucionPasoId, CancellationToken ct)
    {
        var detalle = await db.Set<RpaEjecucionDetalle>().FirstOrDefaultAsync(d => d.EjecucionPasoId == ejecucionPasoId, ct);
        return detalle is not null && detalle.DespliegueId == despliegueId ? detalle : null;
    }
}
