namespace Viariato.Infrastructure.Trabajos;

/// <summary>
/// Reports the lifecycle of a background process (scraper, script, sync) as a <see cref="Trabajo"/>.
/// Any module can inject this without depending on another module — it lives in Infrastructure
/// alongside the generic CRUD kit, as a cross-cutting platform capability.
/// </summary>
public interface ITrabajoTracker
{
    Task<Guid> StartAsync(
        string processCode,
        string? subjectType = null,
        string? subjectKey = null,
        Guid? parentTrabajoId = null,
        CancellationToken ct = default);

    Task RegistrarLogAsync(
        Guid trabajoId,
        string message,
        TrabajoLogLevel level = TrabajoLogLevel.Info,
        CancellationToken ct = default);

    Task ReportProgressAsync(Guid trabajoId, int progressPercent, CancellationToken ct = default);

    Task CompleteAsync(Guid trabajoId, object? data = null, string? summary = null, CancellationToken ct = default);

    Task FailAsync(Guid trabajoId, string error, object? partialData = null, CancellationToken ct = default);
}
