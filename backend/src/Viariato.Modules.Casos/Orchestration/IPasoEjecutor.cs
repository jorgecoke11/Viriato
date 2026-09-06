namespace Viariato.Modules.Casos.Orchestration;

/// <summary>One implementation per <see cref="Flujos.Domain.TipoPaso"/>, resolved by the orchestrator
/// via keyed DI. Lets a single Flujo mix RPA, agentic, API and internal steps without the
/// orchestrator needing to know any of their internals.</summary>
public interface IPasoEjecutor
{
    Task<PasoResultado> EjecutarAsync(PasoEjecucionContext context, CancellationToken ct);
}
