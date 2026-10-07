using Viariato.Modules.Casos.Storage;

namespace Viariato.Modules.Casos.Tests.Storage;

public sealed class LocalDiskDocumentStorageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"viriato-storage-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Folders_SeparateTipoAndGroupByMonthAndCaso()
    {
        var casoId = Guid.Parse("0192b3c4-d5e6-7f80-9a1b-2c3d4e5f6071");
        var at = new DateTimeOffset(2026, 3, 9, 23, 30, 0, TimeSpan.FromHours(-5));

        Assert.Equal("documentos/2026/03/0192b3c4d5e67f809a1b2c3d4e5f6071", StorageFolders.Documentos(casoId, at));
        // 23:30 at UTC-5 on the 9th is already the 10th in UTC — folders always follow UTC.
        Assert.Equal("evidencias/2026/03/0192b3c4d5e67f809a1b2c3d4e5f6071", StorageFolders.Evidencias(casoId, at));
    }

    [Fact]
    public async Task SaveAsync_WithFolder_CreatesItAndRoundTripsTheContent()
    {
        var storage = new LocalDiskDocumentStorage(_root);
        var folder = StorageFolders.Evidencias(Guid.NewGuid(), DateTimeOffset.UtcNow);

        string key;
        await using (var content = new MemoryStream("captura"u8.ToArray()))
        {
            key = await storage.SaveAsync(content, "captura.png", folder, CancellationToken.None);
        }

        Assert.StartsWith(folder + "/", key);
        Assert.EndsWith(".png", key);
        Assert.True(File.Exists(Path.Combine(_root, key)));

        await using var read = await storage.OpenReadAsync(key, CancellationToken.None);
        using var reader = new StreamReader(read);
        Assert.Equal("captura", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task SaveAsync_WithoutFolder_StoresAtTheRootLikeFilesSavedBeforeFolders()
    {
        var storage = new LocalDiskDocumentStorage(_root);

        await using var content = new MemoryStream("x"u8.ToArray());
        var key = await storage.SaveAsync(content, "a.txt", null, CancellationToken.None);

        Assert.DoesNotContain('/', key);
        Assert.True(File.Exists(Path.Combine(_root, key)));
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheFile()
    {
        var storage = new LocalDiskDocumentStorage(_root);

        await using var content = new MemoryStream("x"u8.ToArray());
        var key = await storage.SaveAsync(content, "a.txt", StorageFolders.Documentos(Guid.NewGuid(), DateTimeOffset.UtcNow), CancellationToken.None);
        await storage.DeleteAsync(key, CancellationToken.None);

        Assert.False(File.Exists(Path.Combine(_root, key)));
    }
}
