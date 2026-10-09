namespace Viariato.Modules.Casos.Contracts;

/// <param name="Prioridad">Only for RPA steps (the executions robots take from a queue): higher goes first among the ones
/// waiting for the same service. Null for any other kind of step.</param>
/// <param name="EnCola">The execution is waiting for a robot to take it: this is the only time its priority can change.</param>
/// <param name="ServicioNombre">For an RPA step, the service whose robots run it.</param>
/// <param name="EquipoNombre">For an RPA step a robot has taken, the machine that robot runs on.</param>
/// <param name="ProgresoPorcentaje">While a robot runs the step: how far along it says it is (0–100). Null otherwise.</param>
/// <param name="ProgresoMensaje">While a robot runs the step: what it says it is doing.</param>
/// <param name="VistaEnDirectoUrl">While a robot runs the step: where its screen can be watched, if it said.</param>
public sealed record EjecucionPasoDto(
    Guid Id,
    Guid EjecucionId,
    Guid FlujoPasoDefId,
    string TipoPaso,
    int NumeroIntento,
    string Estado,
    Guid? TrabajoId,
    string? ErrorMensaje,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    int? Prioridad = null,
    bool EnCola = false,
    string? ServicioNombre = null,
    string? EquipoNombre = null,
    int? ProgresoPorcentaje = null,
    string? ProgresoMensaje = null,
    string? VistaEnDirectoUrl = null);
