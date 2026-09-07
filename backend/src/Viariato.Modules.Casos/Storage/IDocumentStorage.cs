namespace Viariato.Modules.Casos.Storage;

/// <summary>
/// Abstracts where a Documento's bytes actually live. The domain only ever holds the opaque
/// <c>storageKey</c> this returns — never a filesystem path or a cloud-provider detail. An instance is
/// resolved per-Flujo by <see cref="IDocumentStorageResolver"/> rather than injected as a single
/// process-wide singleton, since each Flujo can point at a different backend/credentials.
/// </summary>
public interface IDocumentStorage : IAsyncDisposable
{
    Task<string> SaveAsync(Stream content, string suggestedFileName, CancellationToken ct);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);

    Task DeleteAsync(string storageKey, CancellationToken ct);
}
