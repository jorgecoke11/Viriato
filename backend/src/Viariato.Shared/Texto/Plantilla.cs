using System.Text.Json;
using System.Text.RegularExpressions;

namespace Viariato.Shared.Texto;

/// <summary>
/// Fills a text template. Placeholders go in braces: a plain name (<c>{fecha}</c>) takes a value given by the caller, and a dotted one
/// (<c>{datos.cliente.nombre}</c>) goes down into one of the JSON documents given under that first word. A placeholder with nothing to
/// put in it disappears. The case title (PlantillaDeTitulo) and the notifications' subject and body are written with it.
/// </summary>
public static partial class Plantilla
{
    [GeneratedRegex(@"\{([^{}]*)\}")]
    private static partial Regex Marcador();

    public static string Renderizar(
        string plantilla, IReadOnlyDictionary<string, string> valores, IReadOnlyDictionary<string, string?> documentos)
    {
        var raices = new Dictionary<string, JsonElement>();
        foreach (var (nombre, json) in documentos)
        {
            if (string.IsNullOrWhiteSpace(json)) continue;
            try
            {
                using var documento = JsonDocument.Parse(json);
                raices[nombre] = documento.RootElement.Clone();
            }
            catch (JsonException)
            {
                // A document that is not JSON is rejected where it comes from; a text is never the reason to fail.
            }
        }

        return Marcador().Replace(plantilla, coincidencia =>
        {
            var nombre = coincidencia.Groups[1].Value.Trim();
            if (valores.TryGetValue(nombre, out var valor)) return valor;

            var punto = nombre.IndexOf('.');
            if (punto <= 0 || punto == nombre.Length - 1) return string.Empty;
            return raices.TryGetValue(nombre[..punto], out var raiz) ? Valor(raiz, nombre[(punto + 1)..]) : string.Empty;
        });
    }

    /// <summary>The placeholders that mean nothing — neither one of <paramref name="variables"/> nor a field of one of
    /// <paramref name="documentos"/> — so whoever writes the template hears about a typo when saving it.</summary>
    public static IReadOnlyList<string> Desconocidos(string plantilla, IReadOnlyCollection<string> variables, IReadOnlyCollection<string> documentos) =>
        Marcador().Matches(plantilla)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(nombre => !variables.Contains(nombre) && !EsDeDocumento(nombre, documentos))
            .Distinct()
            .ToList();

    private static bool EsDeDocumento(string nombre, IReadOnlyCollection<string> documentos)
    {
        var punto = nombre.IndexOf('.');
        return punto > 0 && punto < nombre.Length - 1 && documentos.Contains(nombre[..punto]);
    }

    private static string Valor(JsonElement raiz, string ruta)
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
