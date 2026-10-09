using System.Text;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>
/// The names inside a zip of documents. A title or a file name is whatever a person (or a robot) wrote, so before it becomes a
/// folder or a file it loses what no file system accepts and anything that could climb out of the folder it is extracted into
/// (<c>..</c>, a slash); and two files that would land on the same name — one differing only in capitals counts, since Windows
/// treats them as the same — get a number instead of overwriting each other.
/// </summary>
internal static class NombresParaZip
{
    private const string SinNombre = "sin nombre";

    /// <summary>The name made safe: invalid characters become <c>_</c>, ends are trimmed (a trailing dot or space is not allowed on
    /// Windows), and it is cut to <paramref name="maximo"/> characters keeping the extension of a file.</summary>
    public static string Sanear(string? nombre, int maximo = 120)
    {
        var limpio = new StringBuilder();
        foreach (var c in nombre ?? string.Empty)
        {
            limpio.Append(char.IsControl(c) || "\\/:*?\"<>|".Contains(c) ? '_' : c);
        }

        var texto = limpio.ToString().Trim().Trim('.').Trim();
        if (texto.Length == 0) return SinNombre;
        if (texto.Length <= maximo) return texto;

        var extension = Path.GetExtension(texto);
        if (extension.Length is > 0 and <= 10 && extension.Length < maximo)
        {
            return texto[..(maximo - extension.Length)].TrimEnd() + extension;
        }

        return texto[..maximo].TrimEnd();
    }

    /// <summary>The folder of a case: its title, plus the start of its id so two cases with the same title do not share a folder.</summary>
    public static string CarpetaDeCaso(string titulo, Guid casoId) => $"{Sanear(titulo, 80)} ({casoId.ToString("N")[..8]})";

    /// <summary>The name, or <c>name (2).ext</c>, <c>name (3).ext</c>… if it is already taken. Takes it.</summary>
    public static string Unico(ISet<string> usados, string nombre)
    {
        if (usados.Add(nombre)) return nombre;

        var extension = Path.GetExtension(nombre);
        var raiz = nombre[..^extension.Length];
        for (var n = 2; ; n++)
        {
            var candidato = $"{raiz} ({n}){extension}";
            if (usados.Add(candidato)) return candidato;
        }
    }

    public static ISet<string> ConjuntoDeNombres() => new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}
