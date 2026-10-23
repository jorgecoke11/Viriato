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
    string GrabacionDeVideo);

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
