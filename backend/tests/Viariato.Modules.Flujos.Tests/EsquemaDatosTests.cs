using Viariato.Modules.Flujos.Esquemas;
using Xunit;

namespace Viariato.Modules.Flujos.Tests;

public sealed class EsquemaDatosTests
{
    // The BSH "Creador de casos" form: a number with a unit and a limit, and a switch.
    private const string Creador = """
        {
          "type": "object",
          "required": ["beneficio"],
          "properties": {
            "beneficio": { "type": "number", "title": "Beneficio", "minimum": 0, "maximum": 100, "x-suffix": "%" },
            "actualizar": { "type": "boolean", "title": "Actualizar precios", "default": false }
          }
        }
        """;

    private static IReadOnlyList<string> Errores(string esquema, string? datos) => EsquemaDatos.ValidarDatos(esquema, datos);

    private static string UnCampo(string campo, bool requerido = false) =>
        $$"""{ "type": "object", "required": [{{(requerido ? "\"x\"" : "")}}], "properties": { "x": {{campo}} } }""";

    // ---------------------------------------------------------------- the schema

    [Fact]
    public void ASchemaWithEveryKindOfField_IsAccepted()
    {
        const string completo = """
            {
              "type": "object",
              "required": ["nombre"],
              "properties": {
                "nombre": { "type": "string", "minLength": 2, "maxLength": 50, "pattern": "^[A-Z]" },
                "notas": { "type": "string", "x-widget": "textarea" },
                "alta": { "type": "string", "format": "date" },
                "momento": { "type": "string", "format": "date-time" },
                "correo": { "type": "string", "format": "email" },
                "estado": { "type": "string", "enum": ["a", "b"], "enumNames": ["A", "B"], "default": "a" },
                "edad": { "type": "integer", "minimum": 0, "maximum": 120 },
                "nivel": { "type": "integer", "enum": [1, 2, 3] },
                "precio": { "type": "number", "x-suffix": "€" },
                "activo": { "type": "boolean" },
                "etiquetas": { "type": "array", "items": { "type": "string" }, "minItems": 1, "maxItems": 5 },
                "tallas": { "type": "array", "items": { "type": "string", "enum": ["S", "M", "L"] } },
                "lineas": { "type": "array", "items": { "type": "object", "required": ["sku"], "properties": {
                  "sku": { "type": "string" }, "unidades": { "type": "integer", "minimum": 1 } } } },
                "direccion": { "type": "object", "properties": { "calle": { "type": "string" } } }
              }
            }
            """;

        Assert.Empty(EsquemaDatos.Validar(completo));
    }

    [Fact]
    public void TheBshCreatorSchema_IsAccepted() => Assert.Empty(EsquemaDatos.Validar(Creador));

    [Theory]
    [InlineData("", "vacío")]
    [InlineData("   ", "vacío")]
    [InlineData("{no es json", "JSON válido")]
    [InlineData("[1]", "objeto JSON")]
    [InlineData("""{"type":"array","properties":{"a":{"type":"string"}}}""", "raíz")]
    [InlineData("""{"type":"object"}""", "properties")]
    [InlineData("""{"type":"object","properties":{}}""", "ningún campo")]
    [InlineData("""{"type":"object","properties":{"a":"string"}}""", "debe ser un objeto")]
    [InlineData("""{"type":"object","properties":{"a":{}}}""", "falta \"type\"")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"uuid"}}}""", "no está soportado")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string"}},"required":["b"]}""", "no existe")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string"}},"required":"a"}""", "lista de nombres")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string","title":3}}}""", "\"title\" debe ser texto")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string","minLength":5,"maxLength":2}}}""", "minLength")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string","minLength":-1}}}""", "mayor o igual que 0")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string","pattern":"("}}}""", "expresión regular")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string","format":"uuid"}}}""", "\"format\"")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string","x-widget":"slider"}}}""", "x-widget")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string","enum":[]}}}""", "al menos una opción")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string","enum":["x",1]}}}""", "no es de tipo string")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"integer","enum":[1.5]}}}""", "no es de tipo integer")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string","enum":["x","y"],"enumNames":["X"]}}}""", "enumNames")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"number","minimum":10,"maximum":1}}}""", "minimum")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"number","minimum":"1"}}}""", "debe ser un número")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"array"}}}""", "falta \"items\"")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"array","items":{"type":"array","items":{"type":"string"}}}}}""", "los elementos de la lista")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"array","items":{"type":"boolean"}}}}""", "los elementos de la lista")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"array","items":{"type":"string"},"minItems":3,"maxItems":1}}}""", "minItems")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"object"}}}""", "properties")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"integer","default":1.5}}}""", "valor por defecto")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"string","enum":["x"],"default":"z"}}}""", "valor por defecto")]
    public void ABadSchema_IsRejected_SayingWhatIsWrong(string esquema, string fragmento)
    {
        var errores = EsquemaDatos.Validar(esquema);

        Assert.NotEmpty(errores);
        Assert.Contains(errores, e => e.Contains(fragmento, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AnErrorInANestedField_NamesItsPath()
    {
        var errores = EsquemaDatos.Validar("""
            {"type":"object","properties":{"lineas":{"type":"array","items":{"type":"object","properties":{"sku":{"type":"uuid"}}}}}}
            """);

        Assert.Contains(errores, e => e.StartsWith("lineas[].sku:"));
    }

    [Fact]
    public void AnObjectNestedTooDeep_IsRejected()
    {
        var esquema = """{"type":"object","properties":{"a":{"type":"object","properties":{"b":{"type":"object","properties":{"c":{"type":"object","properties":{"d":{"type":"object","properties":{"e":{"type":"string"}}}}}}}}}}}""";

        Assert.Contains(EsquemaDatos.Validar(esquema), e => e.Contains("anidado"));
    }

    [Fact]
    public void TooManyFields_AreRejected()
    {
        var campos = string.Join(",", Enumerable.Range(0, 101).Select(i => $"\"c{i}\":{{\"type\":\"string\"}}"));

        var esquema = "{\"type\":\"object\",\"properties\":{" + campos + "}}";

        Assert.Contains(EsquemaDatos.Validar(esquema), e => e.Contains("Demasiados campos"));
    }

    [Fact]
    public void ASchemaThatIsTooLong_IsRejectedBeforeBeingParsed()
    {
        var largo = new string('x', EsquemaDatos.MaxLongitud + 1);

        Assert.Contains(EsquemaDatos.Validar(largo), e => e.Contains("demasiado largo"));
    }

    [Fact]
    public void KeywordsOutsideTheSupportedSubset_AreIgnored_NotRejected()
    {
        const string conExtras = """
            {"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","additionalProperties":false,
             "properties":{"a":{"type":"string","examples":["x"],"readOnly":true}}}
            """;

        Assert.Empty(EsquemaDatos.Validar(conExtras));
    }

    // ---------------------------------------------------------------- the data

    [Fact]
    public void Data_ThatFitsTheSchema_IsAccepted()
    {
        Assert.Empty(Errores(Creador, """{"beneficio":15,"actualizar":true}"""));
        Assert.Empty(Errores(Creador, """{"beneficio":0}"""));
        Assert.Empty(Errores(Creador, """{"beneficio":15.5,"otro":"extra"}"""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("""{"beneficio":null}""")]
    [InlineData("""{"beneficio":""}""")]
    public void MissingData_FailsOnTheRequiredField_UsingItsTitle(string? datos)
    {
        var errores = Errores(Creador, datos);

        Assert.Equal(["Beneficio: es obligatorio."], errores);
    }

    [Theory]
    [InlineData("""{"beneficio":"15"}""", "debe ser un número")]
    [InlineData("""{"beneficio":-1}""", "como mínimo 0")]
    [InlineData("""{"beneficio":101}""", "como máximo 100")]
    [InlineData("""{"beneficio":5,"actualizar":"si"}""", "verdadero o falso")]
    public void WrongValues_AreRejected_WithAMessageSayingWhy(string datos, string fragmento)
    {
        Assert.Contains(Errores(Creador, datos), e => e.Contains(fragmento));
    }

    [Theory]
    [InlineData("[1]", "objeto JSON")]
    [InlineData("\"texto\"", "objeto JSON")]
    [InlineData("{no es json", "JSON válido")]
    public void DataThatIsNotAnObject_IsRejected(string datos, string fragmento)
    {
        Assert.Contains(Errores(Creador, datos), e => e.Contains(fragmento));
    }

    [Fact]
    public void SeveralProblems_AreAllReported_NotJustTheFirst()
    {
        var errores = Errores("""
            {"type":"object","required":["a","b"],"properties":{"a":{"type":"string"},"b":{"type":"integer"},"c":{"type":"boolean"}}}
            """, """{"c":"x"}""");

        Assert.Equal(3, errores.Count);
    }

    [Theory]
    [InlineData("""{"type":"string","minLength":3}""", "\"ab\"", "al menos 3")]
    [InlineData("""{"type":"string","maxLength":3}""", "\"abcd\"", "como máximo 3")]
    [InlineData("""{"type":"string","pattern":"^[A-Z]{2}\\d+$"}""", "\"x1\"", "formato esperado")]
    [InlineData("""{"type":"string","format":"date"}""", "\"2026-13-40\"", "fecha válida")]
    [InlineData("""{"type":"string","format":"date"}""", "\"07/10/2026\"", "fecha válida")]
    [InlineData("""{"type":"string","format":"date-time"}""", "\"ayer\"", "fecha y hora")]
    [InlineData("""{"type":"string","format":"email"}""", "\"sin-arroba\"", "correo")]
    [InlineData("""{"type":"string","enum":["a","b"]}""", "\"c\"", "uno de: a, b")]
    [InlineData("""{"type":"integer"}""", "1.5", "entero")]
    [InlineData("""{"type":"integer","enum":[1,2]}""", "3", "uno de: 1, 2")]
    [InlineData("""{"type":"string"}""", "5", "debe ser texto")]
    [InlineData("""{"type":"boolean"}""", "\"true\"", "verdadero o falso")]
    [InlineData("""{"type":"array","items":{"type":"string"},"minItems":2}""", "[\"a\"]", "al menos 2")]
    [InlineData("""{"type":"array","items":{"type":"string"},"maxItems":1}""", "[\"a\",\"b\"]", "como máximo 1")]
    [InlineData("""{"type":"array","items":{"type":"string"}}""", "\"a\"", "debe ser una lista")]
    [InlineData("""{"type":"array","items":{"type":"string"}}""", "[\"a\",5]", "#2: debe ser texto")]
    [InlineData("""{"type":"array","items":{"type":"string"}}""", "[\"a\",\"\"]", "#2: es obligatorio")]
    [InlineData("""{"type":"object","properties":{"k":{"type":"string"}},"required":["k"]}""", "{}", "k: es obligatorio")]
    [InlineData("""{"type":"object","properties":{"k":{"type":"string"}}}""", "[]", "debe ser un objeto")]
    public void EachKeyword_IsEnforced(string campo, string valor, string fragmento)
    {
        var errores = Errores(UnCampo(campo), $$"""{"x": {{valor}} }""");

        Assert.Contains(errores, e => e.Contains(fragmento));
    }

    [Theory]
    [InlineData("""{"type":"string","minLength":3}""", "\"abc\"")]
    [InlineData("""{"type":"string","pattern":"^[A-Z]{2}\\d+$"}""", "\"AB12\"")]
    [InlineData("""{"type":"string","format":"date"}""", "\"2026-10-07\"")]
    [InlineData("""{"type":"string","format":"date-time"}""", "\"2026-10-07T12:30:00Z\"")]
    [InlineData("""{"type":"string","format":"email"}""", "\"a@b.com\"")]
    [InlineData("""{"type":"string","enum":["a","b"]}""", "\"b\"")]
    [InlineData("""{"type":"integer"}""", "3")]
    [InlineData("""{"type":"integer"}""", "3.0")]
    [InlineData("""{"type":"number","minimum":0.5}""", "0.5")]
    [InlineData("""{"type":"boolean"}""", "false")]
    [InlineData("""{"type":"array","items":{"type":"string"},"minItems":2}""", "[\"a\",\"b\"]")]
    public void ValidValues_PassEachKeyword(string campo, string valor)
    {
        Assert.Empty(Errores(UnCampo(campo), $$"""{"x": {{valor}} }"""));
    }

    [Fact]
    public void AnEmptyOptionalString_IsTreatedAsNotFilledIn()
    {
        Assert.Empty(Errores(UnCampo("""{"type":"string","minLength":3}"""), """{"x":""}"""));
    }

    [Fact]
    public void ARequiredEmptyList_IsMissing()
    {
        var errores = Errores(UnCampo("""{"type":"array","items":{"type":"string"}}""", requerido: true), """{"x":[]}""");

        Assert.Equal(["x: es obligatorio."], errores);
    }

    [Fact]
    public void NestedErrors_NameTheirPathWithLabels()
    {
        const string esquema = """
            {"type":"object","properties":{
              "lineas":{"type":"array","title":"Líneas","items":{"type":"object","required":["sku"],"properties":{
                "sku":{"type":"string","title":"SKU"},"unidades":{"type":"integer","title":"Unidades","minimum":1}}}},
              "direccion":{"type":"object","title":"Dirección","required":["calle"],"properties":{"calle":{"type":"string","title":"Calle"}}}}}
            """;

        var errores = Errores(esquema, """{"lineas":[{"sku":"A","unidades":2},{"unidades":0}],"direccion":{}}""");

        Assert.Contains("Líneas #2 › SKU: es obligatorio.", errores);
        Assert.Contains("Líneas #2 › Unidades: debe ser como mínimo 1.", errores);
        Assert.Contains("Dirección › Calle: es obligatorio.", errores);
        Assert.Equal(3, errores.Count);
    }

    [Fact]
    public void DecimalsAreCheckedWithoutLocaleSurprises()
    {
        var antes = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("es-ES");
            var errores = Errores(UnCampo("""{"type":"number","minimum":0.5}"""), """{"x":0.25}""");

            Assert.Contains("como mínimo 0.5.", errores.Single());
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = antes;
        }
    }
}
