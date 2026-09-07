namespace Viariato.Modules.Flujos.Contracts;

public sealed record FlujoDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    Guid? VersionActivaId,
    int? NumeroVersionActiva,
    bool Activo,
    Guid? StorageConfigId,
    string? StorageConfigNombre,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateFlujoRequest(string Nombre, string? Descripcion);

public sealed record UpdateFlujoRequest(string? Nombre, string? Descripcion, bool? Activo);
