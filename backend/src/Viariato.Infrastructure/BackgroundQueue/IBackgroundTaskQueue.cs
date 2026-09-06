namespace Viariato.Infrastructure.BackgroundQueue;

/// <summary>
/// Minimal in-process work queue so an endpoint can hand off long-running work (a scrape, a sync)
/// without blocking the HTTP request. Not durable across restarts — fine for admin-triggered,
/// on-demand jobs; a durable queue can replace this later without changing callers.
/// </summary>
public interface IBackgroundTaskQueue
{
    void Enqueue(Func<IServiceProvider, CancellationToken, Task> workItem);

    Task<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken ct);
}
