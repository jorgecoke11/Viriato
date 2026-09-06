namespace Viariato.Modules.Casos.Orchestration.Ejecutores;

/// <summary>V1 placeholder: completes immediately. A real timed or event-driven wait needs a
/// scheduler this platform doesn't have yet — until then, use PausarAsync/ReanudarAsync manually
/// for anything that must actually wait on a person or an external event.</summary>
public sealed class EsperaPasoEjecutor : IPasoEjecutor
{
    public Task<PasoResultado> EjecutarAsync(PasoEjecucionContext context, CancellationToken ct) =>
        Task.FromResult(PasoResultado.Completado);
}
