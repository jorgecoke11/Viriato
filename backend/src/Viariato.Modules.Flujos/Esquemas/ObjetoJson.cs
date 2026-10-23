using System.Text.Json;

namespace Viariato.Modules.Flujos.Esquemas;

public static class ObjetoJson
{
    /// <summary>Whether the text is a JSON object (what the data of one Caso is).</summary>
    public static bool Es(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var documento = JsonDocument.Parse(json);
            return documento.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
