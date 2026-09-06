using Viariato.Modules.Markets.Analysis;

namespace Viariato.Modules.Markets.Sourcing;

public interface IFundamentalsSource
{
    Task<IReadOnlyList<FundamentalsYear>> GetFundamentalsAsync(string ticker, CancellationToken ct);
}
