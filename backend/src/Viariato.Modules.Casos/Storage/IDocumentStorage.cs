namespace Viariato.Modules.Casos.Storage;

/// <summary>
/// Abstracts where a Documento's bytes actually live. The domain only ever holds the opaque
/// <c>storageKey</c> this returns — never a filesystem path or a cloud-provider detail — so swapping
/// the local-disk dev implementation for S3/Azure Blob later needs no change to Documento or its
/// endpoints.
/// </summary>
public interface IDocumentStorage
{
    Task<string> SaveAsync(Stream content, string suggestedFileName, CancellationToken ct);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);

    Task DeleteAsync(string storageKey, CancellationToken ct);
}
