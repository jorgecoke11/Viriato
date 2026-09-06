using Microsoft.Extensions.Options;
using Viariato.Shared.Options;

namespace Viariato.Modules.Casos.Storage;

/// <summary>Dev-grade IDocumentStorage backed by the local filesystem. The storage key is a plain
/// generated file name, opaque to callers — nothing outside this class assumes it maps to a path.</summary>
public sealed class LocalDiskDocumentStorage(IOptions<DocumentStorageOptions> options) : IDocumentStorage
{
    public async Task<string> SaveAsync(Stream content, string suggestedFileName, CancellationToken ct)
    {
        var root = ResolveRoot();
        Directory.CreateDirectory(root);

        var storageKey = $"{Guid.CreateVersion7():N}{Path.GetExtension(suggestedFileName)}";
        var path = Path.Combine(root, storageKey);

        await using var fileStream = File.Create(path);
        await content.CopyToAsync(fileStream, ct);

        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var path = Path.Combine(ResolveRoot(), storageKey);
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var path = Path.Combine(ResolveRoot(), storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string ResolveRoot() => Path.GetFullPath(options.Value.LocalPath);
}
