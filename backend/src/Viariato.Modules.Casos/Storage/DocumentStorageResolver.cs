using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Viariato.Infrastructure;
using Viariato.Modules.Flujos.Domain;
using Viariato.Shared.Options;

namespace Viariato.Modules.Casos.Storage;

public sealed class DocumentStorageResolver(AppDbContext db, IOptions<DocumentStorageOptions> defaultOptions) : IDocumentStorageResolver
{
    public async Task<IDocumentStorage> ResolveForFlujoAsync(Guid flujoId, CancellationToken ct)
    {
        var flujo = await db.Set<Flujo>().AsNoTracking()
            .Include(f => f.StorageConfig)
            .FirstOrDefaultAsync(f => f.Id == flujoId, ct);

        var config = flujo?.StorageConfig;

        if (config is null || config.Proveedor == StorageProviderType.Local)
        {
            var root = !string.IsNullOrWhiteSpace(config?.LocalPath) ? config.LocalPath : defaultOptions.Value.LocalPath;
            return new LocalDiskDocumentStorage(root);
        }

        var s3Config = new AmazonS3Config
        {
            ServiceURL = config.Endpoint,
            ForcePathStyle = config.UsePathStyle,
            UseHttp = !config.UseSsl,
            AuthenticationRegion = string.IsNullOrWhiteSpace(config.Region) ? "us-east-1" : config.Region,
        };

        var client = new AmazonS3Client(config.AccessKey, config.SecretKey, s3Config);
        return new S3DocumentStorage(client, config.BucketName!);
    }
}
