using System.Text.Json;

namespace Viariato.Modules.Conexiones.Tipos;

/// <summary>The kinds of account the platform knows (every <see cref="ITipoDeConexion"/> registered), and the checks every kind shares.</summary>
public sealed class TiposDeConexion(IEnumerable<ITipoDeConexion> tipos)
{
    private readonly Dictionary<string, ITipoDeConexion> _porCodigo = tipos.ToDictionary(t => t.Codigo, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ITipoDeConexion> Todos => _porCodigo.Values;

    public ITipoDeConexion? Buscar(string codigo) => _porCodigo.GetValueOrDefault(codigo);

    /// <summary>The problems with some settings for a kind: unknown fields are dropped, required ones must be there, numbers must be
    /// numbers, and then the kind's own checks. Returns the settings as they are stored.</summary>
    public (string? ConfiguracionJson, IReadOnlyList<string> Errores) Normalizar(ITipoDeConexion tipo, JsonElement? configuracion)
    {
        var errores = new List<string>();
        if (configuracion is not { ValueKind: JsonValueKind.Object } objeto)
        {
            return (null, ["La configuración tiene que ser un objeto con los campos de la conexión."]);
        }

        var limpia = new Dictionary<string, object?>();
        foreach (var campo in tipo.Campos)
        {
            var presente = objeto.TryGetProperty(campo.Codigo, out var valor) && valor.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
                && !(valor.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(valor.GetString()));
            if (!presente)
            {
                if (campo.Obligatorio) errores.Add($"Falta «{campo.Etiqueta}».");
                continue;
            }

            switch (campo.Tipo)
            {
                case "numero" when valor.ValueKind == JsonValueKind.Number:
                    limpia[campo.Codigo] = valor.GetDecimal();
                    break;
                case "numero" when valor.ValueKind == JsonValueKind.String && decimal.TryParse(valor.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var numero):
                    limpia[campo.Codigo] = numero;
                    break;
                case "numero":
                    errores.Add($"«{campo.Etiqueta}» tiene que ser un número.");
                    break;
                case "booleano" when valor.ValueKind is JsonValueKind.True or JsonValueKind.False:
                    limpia[campo.Codigo] = valor.GetBoolean();
                    break;
                case "booleano":
                    errores.Add($"«{campo.Etiqueta}» tiene que ser sí o no.");
                    break;
                default:
                    limpia[campo.Codigo] = valor.ValueKind == JsonValueKind.String ? valor.GetString()!.Trim() : valor.GetRawText();
                    break;
            }
        }

        var json = JsonSerializer.Serialize(limpia);
        if (errores.Count == 0)
        {
            using var documento = JsonDocument.Parse(json);
            errores.AddRange(tipo.Validar(documento.RootElement));
        }

        return (json, errores);
    }
}
