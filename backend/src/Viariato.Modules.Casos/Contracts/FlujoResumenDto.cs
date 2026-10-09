namespace Viariato.Modules.Casos.Contracts;

/// <summary>One business estado's count within a Tipo de caso's breakdown. Codigo is null for the
/// "Sin estado" bucket. Never shown at Count 0 — an estado with nothing in it doesn't appear.
/// EsFinal mirrors FlujoEstadoDef.EsFinal (false for "Sin estado") so the UI can tell finalized
/// states apart from in-progress ones without a second lookup.</summary>
public sealed record EstadoConteoDto(string? Codigo, string Display, int Orden, int Count, bool EsFinal);

/// <summary>
/// One Tipo de caso's breakdown within a dashboard card. The Casos fall in exactly one of four groups by what they are
/// doing (their technical estado): EnEjecucion, Pendientes, Detenidos or Finalizados — EnCurso is the first three
/// together. Separately, the same Casos are broken down by their current *business* estado (PorEstado), a different
/// question that does not line up with those groups. TipoCasoId is null for the "Sin tipo" bucket (Casos with none chosen).
/// </summary>
/// <param name="EnEjecucion">Of its Casos, how many a robot (or any executor) is running right now.</param>
/// <param name="Pendientes">Of its Casos, how many are waiting in the queue for a robot to take them.</param>
/// <param name="Detenidos">Of its Casos, how many are neither running nor queued nor over: just started, paused or waiting for a person.</param>
public sealed record TipoCasoConteoDto(
    Guid? TipoCasoId,
    string Nombre,
    int Orden,
    int EnCurso,
    int Finalizados,
    IReadOnlyList<EstadoConteoDto> PorEstado,
    int EnEjecucion = 0,
    int Pendientes = 0,
    int Detenidos = 0);

/// <summary>One dashboard card's worth of data: a Flujo's Casos broken down by Tipo de caso, under
/// whatever date window the caller asked for (or the "activos + finalizados hoy" default).</summary>
public sealed record FlujoResumenDto(
    Guid FlujoId,
    string FlujoNombre,
    int Total,
    IReadOnlyList<TipoCasoConteoDto> PorTipo,
    int ParametrosEditables = 0);
