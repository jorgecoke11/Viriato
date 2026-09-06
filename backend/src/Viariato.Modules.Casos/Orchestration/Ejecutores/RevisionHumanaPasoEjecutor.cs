namespace Viariato.Modules.Casos.Orchestration.Ejecutores;

/// <summary>Parks the step waiting — a person resolves it later via
/// <see cref="IEjecucionOrchestrator.ResolverRevisionAsync"/>, which is what actually continues (or
/// fails) the Ejecucion.</summary>
public sealed class RevisionHumanaPasoEjecutor : IPasoEjecutor
{
    public Task<PasoResultado> EjecutarAsync(PasoEjecucionContext context, CancellationToken ct) =>
        Task.FromResult(PasoResultado.EsperandoRevisionHumana);
}
