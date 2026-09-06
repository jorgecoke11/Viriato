using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>
/// Enforces the AsignacionFlujo security boundary: a user can only read or act on Casos of a Flujo
/// they're explicitly assigned to — on top of, not instead of, the coarse casos.read/manage/review
/// permission claim already required by each endpoint's RequireAuthorization. A missing assignment
/// returns NotFound rather than Forbidden so an unassigned user can't even confirm a Caso exists.
/// </summary>
internal static class FlujoAccessAuthorization
{
    public static Task<bool> TieneAccesoAlFlujoAsync(AppDbContext db, Guid userId, Guid flujoId, CancellationToken ct) =>
        db.Set<AsignacionFlujo>().AnyAsync(a => a.UserId == userId && a.FlujoId == flujoId, ct);

    public static async Task<HashSet<Guid>> FlujosAsignadosAsync(AppDbContext db, Guid userId, CancellationToken ct) =>
        (await db.Set<AsignacionFlujo>().AsNoTracking()
            .Where(a => a.UserId == userId)
            .Select(a => a.FlujoId)
            .ToListAsync(ct))
        .ToHashSet();
}
