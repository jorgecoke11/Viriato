namespace Viariato.ApiContracts;

/// <summary>What a worker gets back after successfully claiming a pending step from the queue.</summary>
public sealed record EjecucionAsignadaDto(
    Guid EjecucionPasoId,
    Guid CasoId,
    string CasoTitulo,
    string FlujoNombre,
    string AplicacionObjetivo,
    string? ParametrosEntrada);
