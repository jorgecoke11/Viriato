using Viariato.Modules.Markets.Sourcing;

namespace Viariato.Modules.Markets.Sync;

public interface IMarketSyncOrchestrator
{
    Task RunAsync(Guid parentTrabajoId, MarketIndex index, CancellationToken ct);
}
