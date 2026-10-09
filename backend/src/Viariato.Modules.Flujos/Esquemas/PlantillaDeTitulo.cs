using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Viariato.Modules.Flujos.Esquemas;

/// <summary>
/// Writes the title of a Caso from a template, for creators that do not ask the user for one. Placeholders go in braces:
/// <c>{creador}</c>, <c>{proceso}</c>, <c>{tipo}</c>, <c>{fecha}</c> (2026-10-08), <c>{hora}</c> (14:30), <c>{n}</c> (the creator's
/// running number) and <c>{datos.campo}</c> for a field of the case's own data (<c>{datos.cliente.nombre}</c> goes down into
/// objects). A placeholder with nothing to put in it (a field the user left empty) just disappears.
/// </summary>
public static partial class PlantillaDeTitulo
{
    public const int LongitudMaxima = 300;

    /// <summary>What a creator uses when its author does not write one.</summary>
    public const string Predeterminada = "{creador} {fecha} #{n}";

    public static readonly IReadOnlyList<string> Variables = ["creador", "proceso", "tipo", "fecha", "hora", "n"];

    [GeneratedRegex(@"\{([^{}]*)\}")]
    private static partial Regex Marcador();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();

    /// <summary>The placeholders in the template that mean nothing, so the author hears about a typo when saving instead of
    /// getting titles with holes in them.</summary>
    public static IReadOnlyList<string> Desconocidos(string plantilla) =>
        Marcador().Matches(plantilla)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(nombre => !Variables.Contains(nombre) && !EsDeDatos(nombre))
            .Distinct()
            .ToList();

    public static string Renderizar(string plantilla, IReadOnlyDictionary<string, string> valores, string? datosJson)
    {
        JsonElement? datos = null;
        if (!string.IsNullOrWhiteSpace(datosJson))
        {
            try
            {
                using var documento = JsonDocument.Parse(datosJson);
                datos = documento.RootElement.Clone();
            }
            catch (JsonException)
            {
                // Data that is not JSON is rejected elsewhere; a title is never the reason to fail.
            }
        }

        var texto = Marcador().Replace(plantilla, coincidencia =>
        {
            var nombre = coincidencia.Groups[1].Value.Trim();
            if (valores.TryGetValue(nombre, out var valor)) return valor;
            return EsDeDatos(nombre) && datos is { } raiz ? ValorDeDatos(raiz, nombre["datos.".Length..]) : string.Empty;
        });

        texto = Espacios().Replace(texto, " ").Trim();
        return texto.Length <= LongitudMaxima ? texto : texto[..LongitudMaxima].TrimEnd();
    }

    private static bool EsDeDatos(string nombre) => nombre.StartsWith("datos.", StringComparison.Ordinal) && nombre.Length > "datos.".Length;

    private static string ValorDeDatos(JsonElement raiz, string ruta)
    {
        var actual = raiz;
        foreach (var parte in ruta.Split('.'))
        {
            if (actual.ValueKind != JsonValueKind.Object || !actual.TryGetProperty(parte, out actual)) return string.Empty;
        }

        return actual.ValueKind switch
        {
            JsonValueKind.String => actual.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => actual.GetRawText(),
            _ => string.Empty,
        };
    }
}
