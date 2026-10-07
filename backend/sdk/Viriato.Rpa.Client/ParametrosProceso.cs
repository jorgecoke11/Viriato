using System.Globalization;

namespace Viriato.Rpa.Client;

/// <summary>
/// The settings of the process (Flujo) a robot belongs to, as set in Viriato's "Parámetros" tab. Codes are
/// matched ignoring case. The <c>...Obligatorio</c> readers fail with a message that says which setting is
/// missing or malformed and where to fix it, so a run stops cleanly instead of working with a wrong value.
/// Plain text, visible to anyone who can read the process: never keep a password here — use credentials.
/// </summary>
public sealed class ParametrosProceso
{
    private readonly Dictionary<string, string> _valores;

    public ParametrosProceso(IEnumerable<KeyValuePair<string, string>> valores)
    {
        ArgumentNullException.ThrowIfNull(valores);
        _valores = new Dictionary<string, string>(valores, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, string> Valores => _valores;

    public bool Contiene(string codigo) => _valores.ContainsKey(codigo);

    /// <summary>The value, or null when the process has no such setting. An empty value is returned as empty.</summary>
    public string? Texto(string codigo) => _valores.GetValueOrDefault(codigo);

    public string TextoObligatorio(string codigo)
    {
        var valor = ValorObligatorio(codigo);
        return string.IsNullOrWhiteSpace(valor) ? throw Vacio(codigo) : valor;
    }

    public int EnteroObligatorio(string codigo)
    {
        var valor = ValorObligatorio(codigo);
        return int.TryParse(valor.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero)
            ? numero
            : throw NoEsNumero(codigo, valor, "un número entero");
    }

    /// <summary>Accepts both "21.5" and "21,5".</summary>
    public decimal DecimalObligatorio(string codigo)
    {
        var valor = ValorObligatorio(codigo);
        return decimal.TryParse(valor.Trim().Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var numero)
            ? numero
            : throw NoEsNumero(codigo, valor, "un número");
    }

    private string ValorObligatorio(string codigo) =>
        _valores.TryGetValue(codigo, out var valor)
            ? valor
            : throw new InvalidOperationException(
                $"El proceso no tiene el parámetro '{codigo}'. Créalo en la pestaña Parámetros del proceso, en Viriato.");

    private static InvalidOperationException Vacio(string codigo) =>
        new($"El parámetro '{codigo}' del proceso está vacío. Ponle un valor en la pestaña Parámetros del proceso, en Viriato.");

    private static InvalidOperationException NoEsNumero(string codigo, string valor, string esperado) =>
        new($"El parámetro '{codigo}' del proceso debería ser {esperado} y vale '{valor}'. Corrígelo en la pestaña Parámetros del proceso, en Viriato.");
}
