using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Viariato.Modules.Flujos.Esquemas;

/// <summary>
/// The description of a JSON document as a form: which fields it has, their type, whether each is required and
/// the limits of its value. It is a deliberately small subset of JSON Schema (so any JSON Schema tool can author
/// it) — enough to render a form and to check what comes back, nothing more:
/// <list type="bullet">
/// <item><c>type</c>: string, number, integer, boolean, array (of string/number/integer/object) and object (nested).</item>
/// <item><c>title</c> (label), <c>description</c> (help text), <c>default</c>, and <c>required</c> on an object.</item>
/// <item>string: <c>minLength</c>, <c>maxLength</c>, <c>pattern</c>, <c>format</c> (date, date-time, email), <c>enum</c> + <c>enumNames</c>,
/// and the hint <c>"x-widget": "textarea"</c>.</item>
/// <item>number/integer: <c>minimum</c>, <c>maximum</c>, <c>enum</c>, and the hint <c>"x-suffix"</c> (a unit shown next to the input).</item>
/// <item>array: <c>items</c>, <c>minItems</c>, <c>maxItems</c>.</item>
/// </list>
/// Keywords outside this list are ignored, so a richer schema still works — it just renders and checks less. The
/// same rules are implemented for the browser (frontend <c>components/schema-form/esquema.ts</c>); keep them in step.
/// The order of <c>properties</c> is the order of the fields in the form, which is why the schema is stored as text.
/// </summary>
public static class EsquemaDatos
{
    public const int MaxLongitud = 20_000;

    private const int MaxProfundidad = 4;
    private const int MaxCampos = 100;

    private static readonly string[] Tipos = ["string", "number", "integer", "boolean", "array", "object"];
    private static readonly string[] TiposDeLista = ["string", "number", "integer", "object"];
    private static readonly string[] Formatos = ["date", "date-time", "email"];

    private static readonly Regex Email = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    /// <summary>Checks that <paramref name="esquemaJson"/> is a schema this platform can render and enforce.
    /// An empty list means it is.</summary>
    public static IReadOnlyList<string> Validar(string esquemaJson)
    {
        if (string.IsNullOrWhiteSpace(esquemaJson)) return ["El esquema está vacío."];
        if (esquemaJson.Length > MaxLongitud) return [$"El esquema es demasiado largo (máximo {MaxLongitud} caracteres)."];

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(esquemaJson);
        }
        catch (JsonException ex)
        {
            return [$"El esquema no es un JSON válido: {ex.Message}"];
        }

        using (documento)
        {
            var errores = new List<string>();
            var campos = 0;
            var raiz = documento.RootElement;

            if (raiz.ValueKind != JsonValueKind.Object)
            {
                return ["El esquema debe ser un objeto JSON."];
            }

            if (raiz.TryGetProperty("type", out var tipo) && !(tipo.ValueKind == JsonValueKind.String && tipo.GetString() == "object"))
            {
                errores.Add("El esquema raíz debe ser de tipo \"object\".");
            }

            ValidarPropiedades(raiz, "", 1, errores, ref campos);
            return errores;
        }
    }

    /// <summary>Checks <paramref name="datosJson"/> against a schema already accepted by <see cref="Validar"/>. Missing
    /// or empty data is an empty object, so a schema with required fields rejects it. Properties the schema does not
    /// mention are allowed (a robot may add its own).</summary>
    public static IReadOnlyList<string> ValidarDatos(string esquemaJson, string? datosJson)
    {
        using var esquema = JsonDocument.Parse(esquemaJson);
        var errores = new List<string>();

        JsonDocument? datos = null;
        try
        {
            if (string.IsNullOrWhiteSpace(datosJson))
            {
                datos = JsonDocument.Parse("{}");
            }
            else
            {
                try
                {
                    datos = JsonDocument.Parse(datosJson);
                }
                catch (JsonException ex)
                {
                    return [$"Los datos no son un JSON válido: {ex.Message}"];
                }
            }

            if (datos.RootElement.ValueKind != JsonValueKind.Object)
            {
                return ["Los datos deben ser un objeto JSON."];
            }

            ValidarObjeto(esquema.RootElement, datos.RootElement, "", errores);
            return errores;
        }
        finally
        {
            datos?.Dispose();
        }
    }

    // ---------------------------------------------------------------- the schema itself

    private static void ValidarPropiedades(JsonElement objeto, string ruta, int profundidad, List<string> errores, ref int campos)
    {
        if (!objeto.TryGetProperty("properties", out var propiedades) || propiedades.ValueKind != JsonValueKind.Object)
        {
            errores.Add($"{Donde(ruta)}falta \"properties\" con los campos.");
            return;
        }

        var nombres = new HashSet<string>(StringComparer.Ordinal);
        foreach (var propiedad in propiedades.EnumerateObject())
        {
            nombres.Add(propiedad.Name);
            campos++;
            if (campos > MaxCampos)
            {
                if (campos == MaxCampos + 1) errores.Add($"Demasiados campos (máximo {MaxCampos}).");
                continue;
            }

            if (string.IsNullOrWhiteSpace(propiedad.Name))
            {
                errores.Add($"{Donde(ruta)}hay un campo sin nombre.");
                continue;
            }

            ValidarCampo(propiedad.Value, Unir(ruta, propiedad.Name), profundidad, errores, ref campos);
        }

        if (nombres.Count == 0)
        {
            errores.Add($"{Donde(ruta)}\"properties\" no tiene ningún campo.");
        }

        if (objeto.TryGetProperty("required", out var requeridos))
        {
            if (requeridos.ValueKind != JsonValueKind.Array)
            {
                errores.Add($"{Donde(ruta)}\"required\" debe ser una lista de nombres de campo.");
                return;
            }

            foreach (var requerido in requeridos.EnumerateArray())
            {
                if (requerido.ValueKind != JsonValueKind.String || !nombres.Contains(requerido.GetString()!))
                {
                    errores.Add($"{Donde(ruta)}\"required\" nombra un campo que no existe: {requerido}.");
                }
            }
        }
    }

    private static void ValidarCampo(JsonElement campo, string ruta, int profundidad, List<string> errores, ref int campos)
    {
        if (campo.ValueKind != JsonValueKind.Object)
        {
            errores.Add($"{ruta}: debe ser un objeto con al menos \"type\".");
            return;
        }

        var erroresAntes = errores.Count;

        if (!campo.TryGetProperty("type", out var tipoElemento) || tipoElemento.ValueKind != JsonValueKind.String)
        {
            errores.Add($"{ruta}: falta \"type\".");
            return;
        }

        var tipo = tipoElemento.GetString()!;
        if (!Tipos.Contains(tipo))
        {
            errores.Add($"{ruta}: el tipo \"{tipo}\" no está soportado (usa {string.Join(", ", Tipos)}).");
            return;
        }

        foreach (var clave in new[] { "title", "description" })
        {
            if (campo.TryGetProperty(clave, out var texto) && texto.ValueKind != JsonValueKind.String)
            {
                errores.Add($"{ruta}: \"{clave}\" debe ser texto.");
            }
        }

        switch (tipo)
        {
            case "string":
                ValidarTexto(campo, ruta, errores);
                break;
            case "number":
            case "integer":
                ValidarNumero(campo, ruta, tipo, errores);
                break;
            case "array":
                ValidarLista(campo, ruta, profundidad, errores, ref campos);
                break;
            case "object":
                if (profundidad >= MaxProfundidad)
                {
                    errores.Add($"{ruta}: demasiado anidado (máximo {MaxProfundidad} niveles).");
                }
                else
                {
                    ValidarPropiedades(campo, ruta, profundidad + 1, errores, ref campos);
                }

                break;
        }

        // A default that the field itself would reject is a trap for whoever fills in the form.
        if (campo.TryGetProperty("default", out var porDefecto) && errores.Count == erroresAntes)
        {
            var erroresDefecto = new List<string>();
            ValidarValor(campo, porDefecto, ruta, obligatorio: false, erroresDefecto);
            foreach (var error in erroresDefecto)
            {
                errores.Add($"{ruta}: el valor por defecto no es válido. {error}");
            }
        }
    }

    private static void ValidarTexto(JsonElement campo, string ruta, List<string> errores)
    {
        var minimo = EnteroNoNegativo(campo, "minLength", ruta, errores);
        var maximo = EnteroNoNegativo(campo, "maxLength", ruta, errores);
        if (minimo > maximo) errores.Add($"{ruta}: \"minLength\" no puede ser mayor que \"maxLength\".");

        if (campo.TryGetProperty("pattern", out var patron))
        {
            if (patron.ValueKind != JsonValueKind.String)
            {
                errores.Add($"{ruta}: \"pattern\" debe ser texto.");
            }
            else
            {
                try
                {
                    _ = new Regex(patron.GetString()!, RegexOptions.None, TimeSpan.FromMilliseconds(200));
                }
                catch (ArgumentException)
                {
                    errores.Add($"{ruta}: \"pattern\" no es una expresión regular válida.");
                }
            }
        }

        if (campo.TryGetProperty("format", out var formato))
        {
            if (formato.ValueKind != JsonValueKind.String || !Formatos.Contains(formato.GetString()))
            {
                errores.Add($"{ruta}: \"format\" no está soportado (usa {string.Join(", ", Formatos)}).");
            }
        }

        if (campo.TryGetProperty("x-widget", out var widget) && !(widget.ValueKind == JsonValueKind.String && widget.GetString() == "textarea"))
        {
            errores.Add($"{ruta}: \"x-widget\" solo admite \"textarea\".");
        }

        ValidarEnumeracion(campo, ruta, "string", errores);
    }

    private static void ValidarNumero(JsonElement campo, string ruta, string tipo, List<string> errores)
    {
        decimal? minimo = null, maximo = null;
        foreach (var (clave, destino) in new[] { ("minimum", 0), ("maximum", 1) })
        {
            if (!campo.TryGetProperty(clave, out var elemento)) continue;
            if (elemento.ValueKind != JsonValueKind.Number || !elemento.TryGetDecimal(out var valor))
            {
                errores.Add($"{ruta}: \"{clave}\" debe ser un número.");
                continue;
            }

            if (destino == 0) minimo = valor; else maximo = valor;
        }

        if (minimo > maximo) errores.Add($"{ruta}: \"minimum\" no puede ser mayor que \"maximum\".");

        if (campo.TryGetProperty("x-suffix", out var sufijo) && sufijo.ValueKind != JsonValueKind.String)
        {
            errores.Add($"{ruta}: \"x-suffix\" debe ser texto.");
        }

        ValidarEnumeracion(campo, ruta, tipo, errores);
    }

    private static void ValidarEnumeracion(JsonElement campo, string ruta, string tipo, List<string> errores)
    {
        if (!campo.TryGetProperty("enum", out var opciones)) return;

        if (opciones.ValueKind != JsonValueKind.Array || opciones.GetArrayLength() == 0)
        {
            errores.Add($"{ruta}: \"enum\" debe ser una lista con al menos una opción.");
            return;
        }

        foreach (var opcion in opciones.EnumerateArray())
        {
            var correcta = tipo == "string"
                ? opcion.ValueKind == JsonValueKind.String
                : opcion.ValueKind == JsonValueKind.Number && (tipo == "number" || EsEntero(opcion));
            if (!correcta) errores.Add($"{ruta}: la opción {opcion} de \"enum\" no es de tipo {tipo}.");
        }

        if (campo.TryGetProperty("enumNames", out var nombres))
        {
            if (nombres.ValueKind != JsonValueKind.Array
                || nombres.GetArrayLength() != opciones.GetArrayLength()
                || nombres.EnumerateArray().Any(n => n.ValueKind != JsonValueKind.String))
            {
                errores.Add($"{ruta}: \"enumNames\" debe ser una lista de textos, uno por cada opción de \"enum\".");
            }
        }
    }

    private static void ValidarLista(JsonElement campo, string ruta, int profundidad, List<string> errores, ref int campos)
    {
        var minimo = EnteroNoNegativo(campo, "minItems", ruta, errores);
        var maximo = EnteroNoNegativo(campo, "maxItems", ruta, errores);
        if (minimo > maximo) errores.Add($"{ruta}: \"minItems\" no puede ser mayor que \"maxItems\".");

        if (!campo.TryGetProperty("items", out var elementos) || elementos.ValueKind != JsonValueKind.Object)
        {
            errores.Add($"{ruta}: falta \"items\" con el tipo de cada elemento de la lista.");
            return;
        }

        var tipoElemento = elementos.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
        if (tipoElemento is null || !TiposDeLista.Contains(tipoElemento))
        {
            errores.Add($"{ruta}: los elementos de la lista deben ser de tipo {string.Join(", ", TiposDeLista)}.");
            return;
        }

        ValidarCampo(elementos, ruta + "[]", profundidad, errores, ref campos);
    }

    private static int? EnteroNoNegativo(JsonElement campo, string clave, string ruta, List<string> errores)
    {
        if (!campo.TryGetProperty(clave, out var elemento)) return null;
        if (elemento.ValueKind != JsonValueKind.Number || !elemento.TryGetInt32(out var valor) || valor < 0)
        {
            errores.Add($"{ruta}: \"{clave}\" debe ser un número entero mayor o igual que 0.");
            return null;
        }

        return valor;
    }

    // ---------------------------------------------------------------- the data

    private static void ValidarObjeto(JsonElement esquema, JsonElement datos, string ruta, List<string> errores)
    {
        var requeridos = esquema.TryGetProperty("required", out var lista) && lista.ValueKind == JsonValueKind.Array
            ? lista.EnumerateArray().Select(r => r.GetString()).ToHashSet()
            : [];

        foreach (var propiedad in esquema.GetProperty("properties").EnumerateObject())
        {
            var presente = datos.TryGetProperty(propiedad.Name, out var valor);
            ValidarValor(
                propiedad.Value, presente ? valor : null, Unir(ruta, Etiqueta(propiedad.Value, propiedad.Name), " › "),
                requeridos.Contains(propiedad.Name), errores);
        }
    }

    private static void ValidarValor(JsonElement esquema, JsonElement? valor, string etiqueta, bool obligatorio, List<string> errores)
    {
        var tipo = esquema.GetProperty("type").GetString()!;

        if (valor is null || valor.Value.ValueKind == JsonValueKind.Null
            || (valor.Value.ValueKind == JsonValueKind.String && valor.Value.GetString() == string.Empty))
        {
            if (obligatorio) errores.Add($"{etiqueta}: es obligatorio.");
            return;
        }

        var v = valor.Value;
        switch (tipo)
        {
            case "string":
                if (v.ValueKind != JsonValueKind.String) { errores.Add($"{etiqueta}: debe ser texto."); return; }
                ValidarTextoValor(esquema, v.GetString()!, etiqueta, errores);
                break;

            case "number":
            case "integer":
                if (v.ValueKind != JsonValueKind.Number || !v.TryGetDecimal(out var numero))
                {
                    errores.Add($"{etiqueta}: debe ser un número{(tipo == "integer" ? " entero" : string.Empty)}.");
                    return;
                }

                if (tipo == "integer" && !EsEntero(v)) { errores.Add($"{etiqueta}: debe ser un número entero."); return; }
                ValidarNumeroValor(esquema, numero, etiqueta, errores);
                break;

            case "boolean":
                if (v.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) errores.Add($"{etiqueta}: debe ser verdadero o falso.");
                break;

            case "array":
                if (v.ValueKind != JsonValueKind.Array) { errores.Add($"{etiqueta}: debe ser una lista."); return; }
                ValidarListaValor(esquema, v, etiqueta, obligatorio, errores);
                break;

            case "object":
                if (v.ValueKind != JsonValueKind.Object) { errores.Add($"{etiqueta}: debe ser un objeto."); return; }
                ValidarObjeto(esquema, v, etiqueta, errores);
                break;
        }
    }

    private static void ValidarTextoValor(JsonElement esquema, string texto, string etiqueta, List<string> errores)
    {
        if (esquema.TryGetProperty("minLength", out var minimo) && texto.Length < minimo.GetInt32())
        {
            errores.Add($"{etiqueta}: debe tener al menos {minimo.GetInt32()} caracteres.");
        }

        if (esquema.TryGetProperty("maxLength", out var maximo) && texto.Length > maximo.GetInt32())
        {
            errores.Add($"{etiqueta}: debe tener como máximo {maximo.GetInt32()} caracteres.");
        }

        if (esquema.TryGetProperty("pattern", out var patron))
        {
            try
            {
                if (!Regex.IsMatch(texto, patron.GetString()!, RegexOptions.None, TimeSpan.FromMilliseconds(200)))
                {
                    errores.Add($"{etiqueta}: no tiene el formato esperado.");
                }
            }
            catch (RegexMatchTimeoutException)
            {
                errores.Add($"{etiqueta}: no se pudo comprobar su formato.");
            }
        }

        if (esquema.TryGetProperty("format", out var formato))
        {
            switch (formato.GetString())
            {
                case "date" when !DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _):
                    errores.Add($"{etiqueta}: debe ser una fecha válida (AAAA-MM-DD).");
                    break;
                case "date-time" when !DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out _):
                    errores.Add($"{etiqueta}: debe ser una fecha y hora válidas.");
                    break;
                case "email" when !Email.IsMatch(texto):
                    errores.Add($"{etiqueta}: debe ser un correo electrónico válido.");
                    break;
            }
        }

        if (esquema.TryGetProperty("enum", out var opciones) && !opciones.EnumerateArray().Any(o => o.GetString() == texto))
        {
            errores.Add($"{etiqueta}: debe ser uno de: {string.Join(", ", opciones.EnumerateArray().Select(o => o.GetString()))}.");
        }
    }

    private static void ValidarNumeroValor(JsonElement esquema, decimal numero, string etiqueta, List<string> errores)
    {
        if (esquema.TryGetProperty("minimum", out var minimo) && numero < minimo.GetDecimal())
        {
            errores.Add($"{etiqueta}: debe ser como mínimo {minimo.GetDecimal().ToString(CultureInfo.InvariantCulture)}.");
        }

        if (esquema.TryGetProperty("maximum", out var maximo) && numero > maximo.GetDecimal())
        {
            errores.Add($"{etiqueta}: debe ser como máximo {maximo.GetDecimal().ToString(CultureInfo.InvariantCulture)}.");
        }

        if (esquema.TryGetProperty("enum", out var opciones) && !opciones.EnumerateArray().Any(o => o.GetDecimal() == numero))
        {
            errores.Add($"{etiqueta}: debe ser uno de: {string.Join(", ", opciones.EnumerateArray().Select(o => o.GetDecimal().ToString(CultureInfo.InvariantCulture)))}.");
        }
    }

    private static void ValidarListaValor(JsonElement esquema, JsonElement lista, string etiqueta, bool obligatorio, List<string> errores)
    {
        var cuantos = lista.GetArrayLength();

        if (cuantos == 0 && obligatorio)
        {
            errores.Add($"{etiqueta}: es obligatorio.");
            return;
        }

        if (esquema.TryGetProperty("minItems", out var minimo) && cuantos < minimo.GetInt32())
        {
            errores.Add($"{etiqueta}: necesita al menos {minimo.GetInt32()} elementos.");
        }

        if (esquema.TryGetProperty("maxItems", out var maximo) && cuantos > maximo.GetInt32())
        {
            errores.Add($"{etiqueta}: admite como máximo {maximo.GetInt32()} elementos.");
        }

        var elementos = esquema.GetProperty("items");
        var indice = 0;
        foreach (var elemento in lista.EnumerateArray())
        {
            indice++;
            // An element has to be there: a null or empty entry in a list is a hole, not "not filled in".
            ValidarValor(elementos, elemento, $"{etiqueta} #{indice}", obligatorio: true, errores);
        }
    }

    // ---------------------------------------------------------------- helpers

    private static bool EsEntero(JsonElement numero) => numero.TryGetDecimal(out var d) && d == decimal.Truncate(d);

    private static string Etiqueta(JsonElement campo, string nombre) =>
        campo.TryGetProperty("title", out var titulo) && titulo.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(titulo.GetString())
            ? titulo.GetString()!
            : nombre;

    private static string Unir(string ruta, string nombre, string separador = ".") => ruta.Length == 0 ? nombre : ruta + separador + nombre;

    private static string Donde(string ruta) => ruta.Length == 0 ? string.Empty : $"{ruta}: ";
}
