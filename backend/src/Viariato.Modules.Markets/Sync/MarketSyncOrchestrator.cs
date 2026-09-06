using Microsoft.Extensions.Options;
using Viariato.Infrastructure.Trabajos;
using Viariato.Modules.Markets.Analysis;
using Viariato.Modules.Markets.Sourcing;
using Viariato.Shared.Options;

namespace Viariato.Modules.Markets.Sync;

/// <summary>
/// Drives one sync run: fetches an index's company list, then for each company scrapes prices +
/// fundamentals and runs <see cref="GrahamAnalyzer"/>, reporting everything through
/// <see cref="ITrabajoTracker"/> — one child Trabajo per company, none of it in a Markets-owned table.
/// A single company failing doesn't abort the run; it's recorded and the sync continues.
/// </summary>
public sealed class MarketSyncOrchestrator(
    ICompanyListSource companyListSource,
    IPriceHistorySource priceHistorySource,
    IFundamentalsSource fundamentalsSource,
    ITrabajoTracker tracker,
    IOptions<MarketsOptions> options) : IMarketSyncOrchestrator
{
    public async Task RunAsync(Guid parentTrabajoId, MarketIndex index, CancellationToken ct)
    {
        var opts = options.Value;

        await tracker.RegistrarLogAsync(parentTrabajoId, $"Obteniendo listado de compañías de {index}…", ct: ct);

        IReadOnlyList<CompanyListItem> companies;
        try
        {
            companies = await companyListSource.GetCompaniesAsync(index, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await tracker.FailAsync(parentTrabajoId, $"No se pudo obtener el listado de compañías: {ex.Message}", ct: ct);
            return;
        }

        await tracker.RegistrarLogAsync(parentTrabajoId, $"{companies.Count} compañías encontradas.", ct: ct);

        var processed = 0;
        var failed = 0;

        foreach (var company in companies)
        {
            ct.ThrowIfCancellationRequested();

            var childId = await tracker.StartAsync(ProcessCodes.AnalyzeCompany, "company", company.Ticker, parentTrabajoId, ct);

            try
            {
                await tracker.RegistrarLogAsync(childId, "Descargando histórico de precios…", ct: ct);
                var prices = await priceHistorySource.GetDailyClosesAsync(company.Ticker, ct);

                await tracker.RegistrarLogAsync(childId, "Descargando fundamentales…", ct: ct);
                var fundamentals = await fundamentalsSource.GetFundamentalsAsync(company.Ticker, ct);

                var profile = new CompanyProfile(company.Ticker, company.Name, company.Sector, company.Industry);
                var result = GrahamAnalyzer.Analyze(profile, fundamentals, prices, opts.DiscountRatePercent);

                await tracker.RegistrarLogAsync(childId, $"Análisis completo: Graham {result.GrahamScore}/7.", ct: ct);
                await tracker.CompleteAsync(childId, result, $"Graham {result.GrahamScore}/7", ct);
                processed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await tracker.RegistrarLogAsync(childId, $"Error: {ex.Message}", TrabajoLogLevel.Error, ct);
                await tracker.FailAsync(childId, ex.Message, ct: ct);
                failed++;
            }

            var progress = (int)((processed + failed) * 100.0 / companies.Count);
            await tracker.ReportProgressAsync(parentTrabajoId, progress, ct);

            if (opts.SyncRequestDelayMs > 0)
            {
                await Task.Delay(opts.SyncRequestDelayMs, ct);
            }
        }

        await tracker.CompleteAsync(
            parentTrabajoId,
            data: new { Scope = index.ToString(), Processed = processed, Failed = failed },
            summary: $"{processed} procesadas, {failed} fallidas",
            ct: ct);
    }
}
