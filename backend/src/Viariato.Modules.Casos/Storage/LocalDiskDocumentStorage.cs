namespace Viariato.Modules.Casos.Storage;

/// <summary>IDocumentStorage backed by the local filesystem. The storage key is a relative path
/// (<c>{folder}/{guid}{ext}</c>, or just the file name for files saved before folders existed),
/// opaque to callers — nothing outside this class assumes it maps to a path.
/// The root is resolved by <see cref="IDocumentStorageResolver"/> (a Flujo's StorageConfig.LocalPath,
/// or the app-wide DocumentStorageOptions default), not injected — there's nothing to dispose.</summary>
public sealed class LocalDiskDocumentStorage(string rootPath) : IDocumentStorage
{
    public async Task<string> SaveAsync(Stream content, string suggestedFileName, string? folder, CancellationToken ct)
    {
        var fileName = $"{Guid.CreateVersion7():N}{Path.GetExtension(suggestedFileName)}";
        var storageKey = string.IsNullOrEmpty(folder) ? fileName : $"{folder}/{fileName}";
        var path = Path.Combine(ResolveRoot(), storageKey);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

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

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private string ResolveRoot() => Path.GetFullPath(rootPath);
}
