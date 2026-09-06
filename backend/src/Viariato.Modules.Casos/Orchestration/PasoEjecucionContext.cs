using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Casos.Orchestration;

/// <summary>Everything an <see cref="IPasoEjecutor"/> needs to run one step. Executors never see the
/// Caso or Flujo directly — only this — so they stay ignorant of orchestration concerns.</summary>
public sealed record PasoEjecucionContext(
    Guid EjecucionPasoId,
    Guid EjecucionId,
    Guid CasoId,
    FlujoPasoDef PasoDef,
    string? DatosJsonCaso);
