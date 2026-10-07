using Viriato.Rpa.Client;

namespace Viriato.Rpa.Client.Tests;

public sealed class ParametrosProcesoTests
{
    private static ParametrosProceso Con(params (string Codigo, string Valor)[] valores) =>
        new(valores.Select(v => KeyValuePair.Create(v.Codigo, v.Valor)));

    [Fact]
    public void CodesAreMatchedIgnoringCase()
    {
        var parametros = Con(("n_maximo_carrito", "14"));

        Assert.True(parametros.Contiene("N_MAXIMO_CARRITO"));
        Assert.Equal("14", parametros.Texto("N_Maximo_Carrito"));
    }

    [Fact]
    public void Texto_ReturnsNullWhenMissing_AndEmptyWhenEmpty()
    {
        var parametros = Con(("cupon", ""));

        Assert.Null(parametros.Texto("no-existe"));
        Assert.Equal("", parametros.Texto("cupon"));
    }

    [Theory]
    [InlineData("14", 14)]
    [InlineData("  14  ", 14)]
    [InlineData("-3", -3)]
    public void EnteroObligatorio_ParsesWholeNumbers(string valor, int esperado)
    {
        Assert.Equal(esperado, Con(("n", valor)).EnteroObligatorio("n"));
    }

    [Theory]
    [InlineData("21", 21)]
    [InlineData("21.5", 21.5)]
    [InlineData("21,5", 21.5)]
    [InlineData(" 4,4 ", 4.4)]
    public void DecimalObligatorio_AcceptsBothDecimalSeparators(string valor, double esperado)
    {
        Assert.Equal((decimal)esperado, Con(("iva", valor)).DecimalObligatorio("iva"));
    }

    [Fact]
    public void DecimalObligatorio_DoesNotDependOnTheMachinesCulture()
    {
        var anterior = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal(21.5m, Con(("iva", "21.5")).DecimalObligatorio("iva"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = anterior;
        }
    }

    [Fact]
    public void AMissingRequiredSetting_SaysWhichOneAndWhereToCreateIt()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Con().EnteroObligatorio("n_maximo_carrito"));

        Assert.Contains("'n_maximo_carrito'", ex.Message);
        Assert.Contains("pestaña Parámetros", ex.Message);
    }

    [Theory]
    [InlineData("catorce")]
    [InlineData("")]
    [InlineData("14.5")]
    public void ANonIntegerValue_IsRejectedWithItsValue(string valor)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Con(("n", valor)).EnteroObligatorio("n"));

        Assert.Contains("'n'", ex.Message);
        Assert.Contains($"'{valor}'", ex.Message);
    }

    [Fact]
    public void ANonNumericDecimal_IsRejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Con(("iva", "mucho")).DecimalObligatorio("iva"));

        Assert.Contains("'mucho'", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TextoObligatorio_RejectsAnEmptyValue(string valor)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Con(("url", valor)).TextoObligatorio("url"));

        Assert.Contains("vacío", ex.Message);
    }

    [Fact]
    public void TextoObligatorio_ReturnsTheValue()
    {
        Assert.Equal("https://x.test", Con(("url", "https://x.test")).TextoObligatorio("url"));
    }
}
