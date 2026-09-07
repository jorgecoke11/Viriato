using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;

namespace Viariato.Modules.Casos.Storage;

/// <summary>
/// IDocumentStorage backed by an S3-compatible bucket (MinIO, AWS S3, Cloudflare R2, Backblaze B2 —
/// they all speak the same API). Owns the <see cref="AmazonS3Client"/> it was handed and disposes it
/// on <see cref="DisposeAsync"/>; callers must NOT dispose it while a stream from
/// <see cref="OpenReadAsync"/> is still being read (see the download endpoint's comment).
/// </summary>
public sealed class S3DocumentStorage(IAmazonS3 client, string bucketName) : IDocumentStorage
{
    public async Task<string> SaveAsync(Stream content, string suggestedFileName, CancellationToken ct)
    {
        await EnsureBucketExistsAsync(ct);

        var storageKey = $"{Guid.CreateVersion7():N}{Path.GetExtension(suggestedFileName)}";
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucketName,
            Key = storageKey,
            InputStream = content,
            AutoCloseStream = false,
        }, ct);

        return storageKey;
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var response = await client.GetObjectAsync(bucketName, storageKey, ct);
        return response.ResponseStream;
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct) => client.DeleteObjectAsync(bucketName, storageKey, ct);

    public ValueTask DisposeAsync()
    {
        client.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task EnsureBucketExistsAsync(CancellationToken ct)
    {
        var exists = await AmazonS3Util.DoesS3BucketExistV2Async(client, bucketName);
        if (!exists)
        {
            await client.PutBucketAsync(new PutBucketRequest { BucketName = bucketName }, ct);
        }
    }
}
