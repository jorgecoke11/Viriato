using Microsoft.Extensions.Options;
using Viariato.Shared.Options;

namespace Viariato.Modules.RpaFleet.Lanzador;

/// <summary>Where the robots' zips are kept on disk: by default a <c>robots</c> folder in the documents' storage, so the nightly
/// backup takes them along with everything else.</summary>
public sealed class AlmacenDeRobots(IOptions<LanzadorOptions> opciones, IOptions<DocumentStorageOptions> documentos)
{
    public string Carpeta => Path.GetFullPath(
        string.IsNullOrWhiteSpace(opciones.Value.CarpetaDeRobots)
            ? Path.Combine(documentos.Value.LocalPath, "robots")
            : opciones.Value.CarpetaDeRobots);

    /// <summary>Copies the zip in and returns its path relative to the folder.</summary>
    public async Task<string> GuardarAsync(Guid despliegueId, Guid versionId, Stream zip, CancellationToken ct)
    {
        var relativa = Path.Combine(despliegueId.ToString("N"), $"{versionId:N}.zip");
        var ruta = Path.Combine(Carpeta, relativa);
        Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
        await using var destino = File.Create(ruta);
        await zip.CopyToAsync(destino, ct);
        return relativa.Replace('\\', '/');
    }

    public Stream Abrir(string relativa) => File.OpenRead(Ruta(relativa));

    public void Borrar(string relativa)
    {
        try
        {
            File.Delete(Ruta(relativa));
        }
        catch (IOException)
        {
            // Best effort: what is left is only disk.
        }
    }

    public void BorrarDelDespliegue(Guid despliegueId)
    {
        try
        {
            var carpeta = Path.Combine(Carpeta, despliegueId.ToString("N"));
            if (Directory.Exists(carpeta)) Directory.Delete(carpeta, recursive: true);
        }
        catch (IOException)
        {
            // Best effort.
        }
    }

    private string Ruta(string relativa)
    {
        var ruta = Path.GetFullPath(Path.Combine(Carpeta, relativa));
        if (!ruta.StartsWith(Carpeta, StringComparison.Ordinal)) throw new InvalidOperationException("Ruta fuera de la carpeta de robots.");
        return ruta;
    }
}
