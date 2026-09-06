namespace Viariato.Modules.Casos.Contracts;

/// <summary>One row in a Caso's list of Ejecuciones — the technical progress history lives one level
/// down, inside each Ejecucion (see EjecucionDto), never flattened into the Caso-level timeline.</summary>
public sealed record EjecucionResumenDto(
    Guid Id,
    Guid CasoId,
    string Estado,
    int PasosCount,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);
