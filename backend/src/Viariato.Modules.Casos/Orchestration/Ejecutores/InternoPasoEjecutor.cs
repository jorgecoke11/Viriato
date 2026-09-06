namespace Viariato.Modules.Casos.Orchestration.Ejecutores;

/// <summary>V1 placeholder: a real deployment replaces this with whatever internal business logic
/// the specific Flujo needs (e.g. a calculation, a lookup). Interno exists so a Flujo can mix pure
/// internal steps with RPA/Agent/API ones without inventing a new TipoPaso each time.</summary>
public sealed class InternoPasoEjecutor : IPasoEjecutor
{
    public Task<PasoResultado> EjecutarAsync(PasoEjecucionContext context, CancellationToken ct) =>
        Task.FromResult(PasoResultado.Completado);
}
