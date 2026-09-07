namespace Viariato.Modules.Flujos.Domain;

public enum StorageProviderType
{
    Local = 0,
    S3Compatible = 1,
}

/// <summary>
/// Where a Flujo's evidencias/documentos actually get written once storage wiring is built (out of
/// scope for this entity — it only owns the config). Credentials never round-trip back to a client;
/// see <c>StorageConfigDto.HasCredentials</c>.
/// </summary>
public sealed class StorageConfig
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Nombre { get; set; }

    public StorageProviderType Proveedor { get; set; }

    // S3Compatible — covers MinIO, AWS S3, Cloudflare R2, Backblaze B2: they all speak the same API.
    public string? Endpoint { get; set; }
    public string? Region { get; set; }
    public string? BucketName { get; set; }
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public bool UsePathStyle { get; set; } = true;
    public bool UseSsl { get; set; } = true;

    // Local
    public string? LocalPath { get; set; }

    public bool Activo { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
