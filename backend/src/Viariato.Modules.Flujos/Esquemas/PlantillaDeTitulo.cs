using System.Text.RegularExpressions;
using Viariato.Shared.Texto;

namespace Viariato.Modules.Flujos.Esquemas;

/// <summary>
/// Writes the title of a Caso from a template, for creators that do not ask the user for one. Placeholders go in braces:
/// <c>{creador}</c>, <c>{proceso}</c>, <c>{tipo}</c>, <c>{fecha}</c> (2026-10-08), <c>{hora}</c> (14:30), <c>{n}</c> (the creator's
/// running number) and <c>{datos.campo}</c> for a field of the case's own data (<c>{datos.cliente.nombre}</c> goes down into
/// objects). A placeholder with nothing to put in it (a field the user left empty) just disappears. A title is one line.
/// </summary>
public static partial class PlantillaDeTitulo
{
    public const int LongitudMaxima = 300;

    /// <summary>What a creator uses when its author does not write one.</summary>
    public const string Predeterminada = "{creador} {fecha} #{n}";

    public static readonly IReadOnlyList<string> Variables = ["creador", "proceso", "tipo", "fecha", "hora", "n"];

    private static readonly string[] Documentos = ["datos"];

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();

    /// <summary>The placeholders in the template that mean nothing, so the author hears about a typo when saving instead of
    /// getting titles with holes in them.</summary>
    public static IReadOnlyList<string> Desconocidos(string plantilla) => Plantilla.Desconocidos(plantilla, Variables.ToArray(), Documentos);

    public static string Renderizar(string plantilla, IReadOnlyDictionary<string, string> valores, string? datosJson)
    {
        var texto = Plantilla.Renderizar(plantilla, valores, new Dictionary<string, string?> { ["datos"] = datosJson });
        texto = Espacios().Replace(texto, " ").Trim();
        return texto.Length <= LongitudMaxima ? texto : texto[..LongitudMaxima].TrimEnd();
    }
}
