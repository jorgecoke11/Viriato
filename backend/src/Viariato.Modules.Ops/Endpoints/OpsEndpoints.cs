using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Infrastructure.Trabajos;
using Viariato.Modules.Ops.Contracts;
using Viariato.Shared;
using Viariato.Shared.Http;

namespace Viariato.Modules.Ops.Endpoints;

/// <summary>
/// Generic monitor over every Trabajo any module has reported — deliberately has no knowledge of
/// what a "markets.sync" or any other process code means, so it works unmodified for whatever
/// scraper or script gets added next.
/// </summary>
public static class OpsEndpoints
{
    public static void MapOpsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/trabajos").RequireAuthorization();

        group.MapGet("/", async (
            string? processCode,
            string? status,
            string? subjectType,
            string? subjectKey,
            int? page,
            int? pageSize,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var currentPage = page is null or < 1 ? 1 : page.Value;
            var currentPageSize = pageSize is null or < 1 or > 100 ? 20 : pageSize.Value;

            var query = db.Set<Trabajo>().AsNoTracking().Where(t => t.ParentTrabajoId == null);

            if (!string.IsNullOrWhiteSpace(processCode)) query = query.Where(t => t.ProcessCode == processCode);
            if (!string.IsNullOrWhiteSpace(subjectType)) query = query.Where(t => t.SubjectType == subjectType);
            if (!string.IsNullOrWhiteSpace(subjectKey)) query = query.Where(t => t.SubjectKey == subjectKey);
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TrabajoStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(t => t.Status == parsedStatus);
            }

            query = query.OrderByDescending(t => t.StartedAt);

            var total = await query.CountAsync(ct);
            var items = await query
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
                .ToListAsync(ct);

            return Results.Ok(new PagedResult<TrabajoListItemDto>(items.Select(ToListItemDto).ToList(), currentPage, currentPageSize, total));
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var trabajo = await db.Set<Trabajo>().AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
            if (trabajo is null) return ProblemResults.NotFound(http, "Trabajo no encontrado.");

            var logs = await db.Set<TrabajoLog>().AsNoTracking()
                .Where(e => e.TrabajoId == id)
                .OrderBy(e => e.Timestamp)
                .Select(e => new TrabajoLogDto(e.Id, e.Timestamp, e.Level.ToString(), e.Message))
                .ToListAsync(ct);

            var children = await db.Set<Trabajo>().AsNoTracking()
                .Where(t => t.ParentTrabajoId == id)
                .OrderBy(t => t.StartedAt)
                .ToListAsync(ct);

            return Results.Ok(new TrabajoDetailDto(
                trabajo.Id,
                trabajo.ProcessCode,
                trabajo.Status.ToString(),
                trabajo.SubjectType,
                trabajo.SubjectKey,
                trabajo.ParentTrabajoId,
                trabajo.Progress,
                trabajo.Summary,
                trabajo.Data,
                trabajo.Error,
                trabajo.StartedAt,
                trabajo.FinishedAt,
                logs,
                children.Select(ToListItemDto).ToList()));
        });
    }

    private static TrabajoListItemDto ToListItemDto(Trabajo t) => new(
        t.Id,
        t.ProcessCode,
        t.Status.ToString(),
        t.SubjectType,
        t.SubjectKey,
        t.ParentTrabajoId,
        t.Progress,
        t.Summary,
        t.Error,
        t.StartedAt,
        t.FinishedAt);
}
