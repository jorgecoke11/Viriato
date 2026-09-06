namespace Viariato.Modules.Casos.Contracts;

/// <summary>One business estado's count within a Tipo de caso's breakdown. Codigo is null for the
/// "Sin estado" bucket. Never shown at Count 0 — an estado with nothing in it doesn't appear.
/// EsFinal mirrors FlujoEstadoDef.EsFinal (false for "Sin estado") so the UI can tell finalized
/// states apart from in-progress ones without a second lookup.</summary>
public sealed record EstadoConteoDto(string? Codigo, string Display, int Orden, int Count, bool EsFinal);

/// <summary>
/// One Tipo de caso's breakdown within a dashboard card — how many of that type are still moving
/// (EnCurso) versus how many reached an active final estado (Finalizados; see FlujoEstadoDef.EsFinal
/// and .Activo), plus the same Casos broken down further by their current business estado. TipoCasoId
/// is null for the "Sin tipo" bucket (Casos with none chosen).
/// </summary>
public sealed record TipoCasoConteoDto(
    Guid? TipoCasoId,
    string Nombre,
    int Orden,
    int EnCurso,
    int Finalizados,
    IReadOnlyList<EstadoConteoDto> PorEstado);

/// <summary>One dashboard card's worth of data: a Flujo's Casos broken down by Tipo de caso, under
/// whatever date window the caller asked for (or the "activos + finalizados hoy" default).</summary>
public sealed record FlujoResumenDto(
    Guid FlujoId,
    string FlujoNombre,
    int Total,
    IReadOnlyList<TipoCasoConteoDto> PorTipo);
