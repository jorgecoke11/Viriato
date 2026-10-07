namespace Viariato.Modules.Casos.Storage;

/// <summary>
/// Folder layout inside a storage root: <c>{tipo}/{año}/{mes}/{casoId}/</c>. Evidencias (screenshots,
/// logs, recordings — big and expendable) live apart from Documentos (the expediente — small and
/// valuable) so they can be backed up, moved or purged on their own; year/month keeps directories
/// small and lets retention drop a whole month; the caso folder keeps one case's files together.
/// </summary>
public static class StorageFolders
{
    public static string Documentos(Guid casoId, DateTimeOffset at) => Build("documentos", casoId, at);

    public static string Evidencias(Guid casoId, DateTimeOffset at) => Build("evidencias", casoId, at);

    private static string Build(string tipo, Guid casoId, DateTimeOffset at)
    {
        var utc = at.UtcDateTime;
        return $"{tipo}/{utc:yyyy}/{utc:MM}/{casoId:N}";
    }
}
