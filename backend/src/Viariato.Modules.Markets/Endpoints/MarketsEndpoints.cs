using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Infrastructure.BackgroundQueue;
using Viariato.Infrastructure.Trabajos;
using Viariato.Modules.Markets.Analysis;
using Viariato.Modules.Markets.Contracts;
using Viariato.Modules.Markets.Sourcing;
using Viariato.Modules.Markets.Sync;
using Viariato.Shared;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Markets.Endpoints;

public static class MarketsEndpoints
{
    public static void MapMarketsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/markets").RequireAuthorization();

        group.MapGet("/companies", GetCompaniesAsync);
        group.MapGet("/companies/{ticker}", GetCompanyAsync);

        endpoints.MapPost("/api/v1/markets/sync", TriggerSyncAsync).RequireAuthorization(Permissions.MarketsManage);
    }

    private static async Task<IResult> GetCompaniesAsync(
        string? search,
        string? index,
        string? sector,
        int? minScore,
        int? page,
        int? pageSize,
        AppDbContext db,
        CancellationToken ct)
    {
        var currentPage = page is null or < 1 ? 1 : page.Value;
        var currentPageSize = pageSize is null or < 1 or > 100 ? 20 : pageSize.Value;

        // Each sync run creates a fresh child Trabajo per company rather than updating one in
        // place, so "the current view" is always the most recently completed analysis per ticker.
        var raw = await (
            from child in db.Set<Trabajo>().AsNoTracking()
            where child.ProcessCode == ProcessCodes.AnalyzeCompany
                && child.Status == TrabajoStatus.Completed
                && child.SubjectKey != null
                && child.Data != null
            join p in db.Set<Trabajo>().AsNoTracking() on child.ParentTrabajoId equals p.Id into parentGroup
            from parent in parentGroup.DefaultIfEmpty()
            select new { child.SubjectKey, child.Data, child.FinishedAt, IndexScope = parent != null ? parent.SubjectKey : null }
        ).ToListAsync(ct);

        var analyses = raw
            .GroupBy(r => r.SubjectKey!)
            .Select(g => g.OrderByDescending(r => r.FinishedAt).First())
            .Select(r => new LatestAnalysisRow(r.IndexScope, JsonSerializer.Deserialize<CompanyAnalysisResult>(r.Data!)))
            .Where(r => r.Analysis is not null)
            .ToList();

        IEnumerable<LatestAnalysisRow> query = analyses;

        if (!string.IsNullOrWhiteSpace(index))
        {
            query = query.Where(r => string.Equals(r.IndexScope, index, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(sector))
        {
            query = query.Where(r => r.Analysis!.Sector.Contains(sector, StringComparison.OrdinalIgnoreCase));
        }

        if (minScore is not null)
        {
            query = query.Where(r => r.Analysis!.GrahamScore >= minScore.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r =>
                r.Analysis!.Ticker.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                r.Analysis.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = query
            .OrderByDescending(r => r.Analysis!.GrahamScore)
            .ThenBy(r => r.Analysis!.Ticker)
            .ToList();

        var total = ordered.Count;
        var items = ordered
            .Skip((currentPage - 1) * currentPageSize)
            .Take(currentPageSize)
            .Select(r => ToListItemDto(r.Analysis!))
            .ToList();

        return Results.Ok(new PagedResult<CompanyListItemDto>(items, currentPage, currentPageSize, total));
    }

    private static async Task<IResult> GetCompanyAsync(string ticker, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var normalized = ticker.Trim().ToUpperInvariant();

        var trabajo = await db.Set<Trabajo>().AsNoTracking()
            .Where(t => t.ProcessCode == ProcessCodes.AnalyzeCompany
                && t.Status == TrabajoStatus.Completed
                && t.SubjectKey == normalized
                && t.Data != null)
            .OrderByDescending(t => t.FinishedAt)
            .FirstOrDefaultAsync(ct);

        if (trabajo is null)
        {
            return ProblemResults.NotFound(http, "No hay análisis disponible para esta compañía todavía.");
        }

        var analysis = JsonSerializer.Deserialize<CompanyAnalysisResult>(trabajo.Data!)!;
        return Results.Ok(new CompanyDetailDto(trabajo.Id, analysis));
    }

    private static async Task<IResult> TriggerSyncAsync(
        TriggerSyncRequest request,
        ITrabajoTracker tracker,
        IBackgroundTaskQueue queue,
        HttpContext http,
        CancellationToken ct)
    {
        if (!Enum.TryParse<MarketIndex>(request.Scope, true, out var index))
        {
            return ProblemResults.Conflict(http, "Scope inválido. Usa 'Sp500' o 'Ndx100'.");
        }

        var trabajoId = await tracker.StartAsync(ProcessCodes.Sync, "index", index.ToString(), ct: ct);

        queue.Enqueue(async (services, workCt) =>
        {
            var orchestrator = services.GetRequiredService<IMarketSyncOrchestrator>();
            await orchestrator.RunAsync(trabajoId, index, workCt);
        });

        return Results.Accepted(value: new TriggerSyncResponse(trabajoId));
    }

    private static CompanyListItemDto ToListItemDto(CompanyAnalysisResult a) => new(
        a.Ticker, a.Name, a.Sector, a.GrahamScore, a.CriteriaEvaluated, a.PeRatio, a.PbRatio, a.MarginOfSafetyPercent, a.Signal.ToString(), a.ComputedAt);

    private sealed record LatestAnalysisRow(string? IndexScope, CompanyAnalysisResult? Analysis);
}
