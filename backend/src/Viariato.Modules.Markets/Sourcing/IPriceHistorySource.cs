using Viariato.Modules.Markets.Analysis;

namespace Viariato.Modules.Markets.Sourcing;

public interface IPriceHistorySource
{
    Task<IReadOnlyList<PricePoint>> GetDailyClosesAsync(string ticker, CancellationToken ct);
}
