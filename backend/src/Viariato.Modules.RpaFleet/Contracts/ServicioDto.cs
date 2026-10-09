namespace Viariato.Modules.RpaFleet.Contracts;

public sealed record ServicioDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    bool Activo,
    int? MaxEjecucionesGlobales,
    int? TiempoMaximoMinutos,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateServicioRequest(
    string Nombre, string? Descripcion, int? MaxEjecucionesGlobales = null, int? TiempoMaximoMinutos = null);

/// <param name="MaxEjecucionesGlobales">Sets the global cap (null leaves it as it is).</param>
/// <param name="QuitarLimiteGlobal">True removes the cap.</param>
/// <param name="TiempoMaximoMinutos">Sets the longest a step may be in execution (null leaves it as it is).</param>
/// <param name="QuitarTiempoMaximo">True removes the time limit.</param>
public sealed record UpdateServicioRequest(
    string? Nombre,
    string? Descripcion,
    bool? Activo,
    int? MaxEjecucionesGlobales = null,
    bool QuitarLimiteGlobal = false,
    int? TiempoMaximoMinutos = null,
    bool QuitarTiempoMaximo = false);
