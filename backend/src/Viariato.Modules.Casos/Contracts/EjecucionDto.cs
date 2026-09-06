namespace Viariato.Modules.Casos.Contracts;

public sealed record EjecucionDto(
    Guid Id,
    Guid CasoId,
    Guid FlujoVersionId,
    string Estado,
    Guid? PasoActualId,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<EjecucionPasoDto> Pasos);
