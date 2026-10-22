using System.Text.Json;
using System.Text.Json.Nodes;

namespace Viariato.Modules.Flujos.Esquemas;

/// <summary>
/// Lays one set of data over another, field by field: what a creator's defaults and a launcher's (or a robot's) data make together.
/// A field the top one has wins; one it does not have keeps the base's value. Objects are merged the same way, all the way down; any
/// other value — a list included — is replaced whole (a list of three is not "the first three of the base's five").
/// </summary>
public static class MezclaDeDatos
{
    /// <summary>The merged JSON object, or null when neither has data. Both are expected to be JSON objects (null or blank: none).</summary>
    public static string? Mezclar(string? baseJson, string? encimaJson)
    {
        if (string.IsNullOrWhiteSpace(baseJson)) return string.IsNullOrWhiteSpace(encimaJson) ? null : encimaJson;
        if (string.IsNullOrWhiteSpace(encimaJson)) return baseJson;

        if (JsonNode.Parse(baseJson) is not JsonObject resultado || JsonNode.Parse(encimaJson) is not JsonObject encima) return encimaJson;

        Fundir(resultado, encima);
        return resultado.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    }

    private static void Fundir(JsonObject destino, JsonObject encima)
    {
        foreach (var (clave, valor) in encima.ToList())
        {
            if (valor is JsonObject hijo && destino[clave] is JsonObject hijoDestino)
            {
                Fundir(hijoDestino, hijo);
                continue;
            }

            destino[clave] = valor?.DeepClone();
        }
    }
}
