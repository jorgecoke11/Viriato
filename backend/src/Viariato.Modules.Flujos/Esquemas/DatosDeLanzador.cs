using System.Text.Json;

namespace Viariato.Modules.Flujos.Esquemas;

/// <summary>
/// The data a launcher gives to the cases it opens. A JSON <b>object</b> is the data of one case. A JSON <b>list of objects</b> opens one case
/// per element, each with its own data (and its own title, written from it): "analyse these five values" is five cases, one per value, not one
/// case that does five things. No data at all opens one case without data.
/// </summary>
public static class DatosDeLanzador
{
    public const int MaximoDeElementos = 200;

    /// <summary>The data of each case to open, as JSON text (null for "no data"). False, with the reason, if it is neither an object nor a
    /// non-empty list of objects.</summary>
    public static bool TryElementos(string? datosJson, out IReadOnlyList<string?> elementos, out string? error)
    {
        elementos = [];
        error = null;
        if (string.IsNullOrWhiteSpace(datosJson))
        {
            elementos = [null];
            return true;
        }

        try
        {
            using var documento = JsonDocument.Parse(datosJson);
            var raiz = documento.RootElement;
            switch (raiz.ValueKind)
            {
                case JsonValueKind.Object:
                    elementos = [raiz.GetRawText()];
                    return true;

                case JsonValueKind.Array:
                {
                    var lista = new List<string?>();
                    foreach (var elemento in raiz.EnumerateArray())
                    {
                        if (elemento.ValueKind != JsonValueKind.Object)
                        {
                            error = $"Cada elemento de la lista tiene que ser un objeto JSON, p. ej. {{\"ticker\": \"AAPL\"}}; el elemento {lista.Count + 1} no lo es.";
                            return false;
                        }

                        lista.Add(elemento.GetRawText());
                    }

                    if (lista.Count == 0)
                    {
                        error = "La lista está vacía: no habría ningún caso que abrir.";
                        return false;
                    }

                    if (lista.Count > MaximoDeElementos)
                    {
                        error = $"La lista tiene {lista.Count} elementos y el máximo es {MaximoDeElementos}.";
                        return false;
                    }

                    elementos = lista;
                    return true;
                }

                default:
                    error = "Los datos tienen que ser un objeto JSON (un caso) o una lista de objetos (un caso por elemento).";
                    return false;
            }
        }
        catch (JsonException)
        {
            error = "Los datos no son un JSON válido.";
            return false;
        }
    }
}
