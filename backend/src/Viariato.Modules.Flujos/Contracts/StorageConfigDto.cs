using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Contracts;

public sealed record StorageConfigDto(
    Guid Id,
    string Nombre,
    StorageProviderType Proveedor,
    string? Endpoint,
    string? Region,
    string? BucketName,
    bool HasCredentials,
    bool UsePathStyle,
    bool UseSsl,
    string? LocalPath,
    bool Activo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateStorageConfigRequest(
    string Nombre,
    StorageProviderType Proveedor,
    string? Endpoint,
    string? Region,
    string? BucketName,
    string? AccessKey,
    string? SecretKey,
    bool UsePathStyle,
    bool UseSsl,
    string? LocalPath);

public sealed record UpdateStorageConfigRequest(
    string? Nombre,
    string? Endpoint,
    string? Region,
    string? BucketName,
    string? AccessKey,
    string? SecretKey,
    bool? UsePathStyle,
    bool? UseSsl,
    string? LocalPath,
    bool? Activo);

public sealed record UpdateFlujoAlmacenamientoRequest(Guid? StorageConfigId);
