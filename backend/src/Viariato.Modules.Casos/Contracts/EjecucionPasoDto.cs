namespace Viariato.Modules.Casos.Contracts;

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
    DateTimeOffset? FinishedAt);
