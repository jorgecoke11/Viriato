using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Flujos.Contracts;
using Viariato.Modules.Flujos.Domain;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Flujos.Endpoints;

/// <summary>
/// Who can see a Flujo's Casos at all — a real access-control boundary Casos enforces on every
/// endpoint, not a display preference. Managed here (FlujosManage) since it's a property of the
/// Flujo itself; <see cref="GetMisFlujosAsignadosAsync"/> is what a Casos dashboard calls to know
/// which Flujo cards the current user is even allowed to see.
/// </summary>
internal static class AsignacionesEndpoints
{
    public static void MapAsignacionesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/flujos/asignados", GetMisFlujosAsignadosAsync)
            .RequireAuthorization(Permissions.CasosRead);

        var manage = endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/asignaciones").RequireAuthorization(Permissions.FlujosManage);
        manage.MapGet("/", ListAsignacionesAsync);
        manage.MapPost("/", AsignarAsync);
        manage.MapDelete("/{userId:guid}", DesasignarAsync);
    }

    private static async Task<IResult> GetMisFlujosAsignadosAsync(AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var userId = http.User.GetUserId();

        var flujos = await db.Set<AsignacionFlujo>().AsNoTracking()
            .Where(a => a.UserId == userId)
            .Join(db.Set<Flujo>().AsNoTracking(), a => a.FlujoId, f => f.Id, (a, f) => f)
            .Include(f => f.VersionActiva)
            .OrderBy(f => f.Nombre)
            .ToListAsync(ct);

        return Results.Ok(flujos.Select(f => f.ToDto()).ToList());
    }

    private static async Task<IResult> ListAsignacionesAsync(Guid flujoId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var flujoExiste = await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct);
        if (!flujoExiste) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        var asignaciones = await db.Set<AsignacionFlujo>().AsNoTracking()
            .Where(a => a.FlujoId == flujoId)
            .Select(a => a.ToDto())
            .ToListAsync(ct);

        return Results.Ok(asignaciones);
    }

    private static async Task<IResult> AsignarAsync(
        Guid flujoId, CreateAsignacionRequest request, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var flujoExiste = await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct);
        if (!flujoExiste) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        var yaAsignado = await db.Set<AsignacionFlujo>().AnyAsync(a => a.FlujoId == flujoId && a.UserId == request.UserId, ct);
        if (yaAsignado) return ProblemResults.Conflict(http, "Ese usuario ya tiene asignado este flujo.");

        var asignacion = new AsignacionFlujo
        {
            FlujoId = flujoId,
            UserId = request.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = http.User.GetUserId(),
        };
        db.Add(asignacion);
        await db.SaveChangesAsync(ct);

        return Results.Ok(asignacion.ToDto());
    }

    private static async Task<IResult> DesasignarAsync(Guid flujoId, Guid userId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var asignacion = await db.Set<AsignacionFlujo>().FirstOrDefaultAsync(a => a.FlujoId == flujoId && a.UserId == userId, ct);
        if (asignacion is null) return ProblemResults.NotFound(http, "Asignación no encontrada.");

        db.Remove(asignacion);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }
}
