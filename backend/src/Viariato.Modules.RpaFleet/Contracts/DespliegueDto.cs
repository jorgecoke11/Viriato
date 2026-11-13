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
    int? VersionDeRobotNumero = null,
    int Replicas = 1,
    int? MemoriaMaximaMb = null,
    bool EnElLanzador = false,
    Guid? VersionFijadaId = null);

/// <summary>One version of a service's robot, uploaded for the launcher next to Viriato.</summary>
/// <param name="Ultima">It is the latest: what every Despliegue not held at another one runs.</param>
/// <param name="Ejecuta">How it is run, as worked out from the zip, e.g. <c>dotnet Bsh.PricesExtractor.dll</c>.</param>
/// <param name="UsadaPor">The machines whose Despliegue the launcher runs with it, e.g. <c>Equipo 1</c>.</param>
public sealed record VersionDeRobotDto(
    Guid Id,
    int Numero,
    string NombreArchivo,
    long TamanoBytes,
    string Ejecuta,
    bool Pantalla,
    int? MemoriaMaximaMb,
    string? SubidaPor,
    DateTimeOffset CreatedAt,
    bool Ultima,
    IReadOnlyList<string> UsadaPor);

/// <summary>Whether the launcher next to Viriato runs this Despliegue's robot, and with which of its service's versions.</summary>
/// <param name="EnElLanzador">False leaves the robot to run somewhere else, with its key.</param>
/// <param name="VersionId">The version to hold it at; null follows the latest one uploaded.</param>
public sealed record RobotDelDespliegueRequest(bool EnElLanzador, Guid? VersionId);

/// <summary>How the launcher runs this Despliegue's robot.</summary>
/// <param name="Replicas">Copies at once when it has work; 0 parks it.</param>
/// <param name="MemoriaMaximaMb">Ceiling of each copy; null = what its version says (2048 MB with a browser, none without).</param>
public sealed record RobotEnElLanzadorRequest(int Replicas, int? MemoriaMaximaMb);

/// <summary>How much the launcher next to Viriato may run at once on its machine.</summary>
public sealed record AjustesDelLanzadorDto(int MaxRobotsALaVez, int MaxConNavegador);

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
