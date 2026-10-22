using Microsoft.AspNetCore.StaticFiles;

namespace Viariato.Modules.Casos.Storage;

/// <summary>
/// What a stored file is. Robots (the SDK) upload files without saying, which arrives as <c>application/octet-stream</c>: then the
/// name's extension decides, so a chart uploaded as <c>grafico.png</c> is shown as an image in the case instead of as a download.
/// </summary>
internal static class TipoDeContenido
{
    private const string Generico = "application/octet-stream";
    private static readonly FileExtensionContentTypeProvider PorExtension = new();

    public static string De(string? declarado, string nombreArchivo)
    {
        if (!string.IsNullOrWhiteSpace(declarado) && !string.Equals(declarado, Generico, StringComparison.OrdinalIgnoreCase)) return declarado;
        return PorExtension.TryGetContentType(nombreArchivo, out var deducido) ? deducido : Generico;
    }
}
