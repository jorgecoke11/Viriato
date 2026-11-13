namespace Viariato.Modules.RpaFleet.Contracts;

public sealed record DespliegueDto(
    Guid Id,
    Guid EquipoId,
    string EquipoNombre,
    Guid ServicioId,
    string ServicioNombre,
    Guid FlujoId,
    Guid? FlujoDestinoId,
    bool Encendido,
    string ApiKeyPrefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastUsedAt,
    string VistaEnDirecto,
    string GrabacionDeVideo,
    int? VersionDeRobotNumero = null);

/// <summary>One version of a robot uploaded to a Despliegue (for the launcher next to Viriato).</summary>
/// <param name="Actual">It is the one the launcher runs.</param>
/// <param name="Ejecuta">How it is run, as worked out from the zip, e.g. <c>dotnet Bsh.PricesExtractor.dll</c>.</param>
public sealed record VersionDeRobotDto(
    Guid Id,
    int Numero,
    string NombreArchivo,
    long TamanoBytes,
    string Ejecuta,
    bool Pantalla,
    int? MemoriaMaximaMb,
    int MaxCopias,
    string? SubidaPor,
    DateTimeOffset CreatedAt,
    bool Actual);

/// <param name="VersionId">The version the launcher runs from now on; null for it to stop running this Despliegue.</param>
public sealed record UsarVersionDeRobotRequest(Guid? VersionId);

/// <summary>Returned only once, right after creating/regenerating a Despliegue's key — never
/// retrievable again afterwards.</summary>
public sealed record DespliegueConApiKeyDto(DespliegueDto Despliegue, string ApiKey);

public sealed record CreateDespliegueRequest(Guid EquipoId, Guid ServicioId, Guid FlujoId, Guid? FlujoDestinoId = null);

/// <param name="FlujoDestinoId">Sets the process this robot may create Casos in.</param>
/// <param name="QuitarFlujoDestino">True to remove that permission (null in <paramref name="FlujoDestinoId"/> means "leave as is").</param>
/// <param name="VistaEnDirecto"><c>Apagada</c>, <c>Baja</c>, <c>Media</c> or <c>Alta</c>; null leaves it as is.</param>
/// <param name="GrabacionDeVideo"><c>SegunRobot</c>, <c>Activa</c> or <c>Apagada</c>; null leaves it as is.</param>
public sealed record UpdateDespliegueRequest(
    bool? Encendido, Guid? FlujoDestinoId = null, bool QuitarFlujoDestino = false, string? VistaEnDirecto = null, string? GrabacionDeVideo = null);
