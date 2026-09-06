namespace Viariato.Modules.Casos.Orchestration;

/// <summary>What an <see cref="IPasoEjecutor"/> reports back to the orchestrator immediately.
/// <see cref="EnProgreso"/> means the executor dispatched real async work (e.g. via the background
/// queue) and will finalize the EjecucionPaso's Estado itself before calling back into
/// <see cref="IEjecucionOrchestrator.AvanzarAsync"/> — the orchestrator does nothing further until then.</summary>
public enum PasoResultado
{
    Completado,
    Fallido,
    EnProgreso,
    EsperandoRevisionHumana,
}
