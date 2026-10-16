namespace Viariato.Modules.Flujos.Contracts;

public sealed record LanzadorDeCasoDto(
    Guid Id,
    Guid FlujoId,
    Guid CreadorDeCasoId,
    string CreadorNombre,
    string Nombre,
    string? Descripcion,
    string? DatosJson,
    string Cron,
    string ZonaHoraria,
    int ToleranciaMinutos,
    bool Activo,
    DateTimeOffset? ProximoDisparoEn,
    DateTimeOffset? UltimoDisparoEn,
    Guid? UltimoCasoId,
    string? UltimoError,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>The whole launcher, for both creating and replacing one.</summary>
/// <param name="Cron">Crontab expressions of five fields, one per line; it fires when any matches.</param>
/// <param name="ZonaHoraria">IANA name of the zone the cron is read in.</param>
/// <param name="ToleranciaMinutos">How late a moment can still fire (1 to 1440).</param>
public sealed record GuardarLanzadorDeCasoRequest(
    string Nombre,
    string? Descripcion,
    Guid CreadorDeCasoId,
    string? DatosJson,
    string Cron,
    string ZonaHoraria,
    int ToleranciaMinutos = 120,
    bool Activo = true);
