namespace Viariato.Modules.Casos.Orchestration.Ejecutores;

/// <summary>Always "succeeds" — a Decision step evaluates which branch to take, it doesn't do work
/// that can fail. The actual branching happens centrally in EjecucionOrchestrator once this step is
/// marked Completado, by reading the same FlujoPasoDef.ConfiguracionJson this executor was given.</summary>
public sealed class DecisionPasoEjecutor : IPasoEjecutor
{
    public Task<PasoResultado> EjecutarAsync(PasoEjecucionContext context, CancellationToken ct) =>
        Task.FromResult(PasoResultado.Completado);
}
