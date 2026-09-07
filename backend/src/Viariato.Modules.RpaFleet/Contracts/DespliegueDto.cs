namespace Viariato.Modules.RpaFleet.Contracts;

public sealed record DespliegueDto(
    Guid Id,
    Guid EquipoId,
    string EquipoNombre,
    Guid ServicioId,
    string ServicioNombre,
    Guid FlujoId,
    bool Encendido,
    string ApiKeyPrefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastUsedAt);

/// <summary>Returned only once, right after creating/regenerating a Despliegue's key — never
/// retrievable again afterwards.</summary>
public sealed record DespliegueConApiKeyDto(DespliegueDto Despliegue, string ApiKey);

public sealed record CreateDespliegueRequest(Guid EquipoId, Guid ServicioId, Guid FlujoId);

public sealed record UpdateDespliegueRequest(bool? Encendido);
