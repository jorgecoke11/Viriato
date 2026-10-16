using Viariato.Modules.Casos.Endpoints;
using Xunit;

namespace Viariato.Modules.Casos.Tests;

public sealed class PaseDeVistaTests
{
    private const string Clave = "una-clave-de-firma-de-al-menos-32-bytes";
    private static readonly Guid Usuario = Guid.Parse("0192a000-0000-7000-8000-000000000001");
    private static readonly DateTimeOffset Ahora = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Vale_hasta_que_caduca()
    {
        var pase = PaseDeVista.Emitir(Clave, Usuario, Ahora.AddHours(1));

        Assert.True(PaseDeVista.EsValido(Clave, pase, Ahora));
        Assert.True(PaseDeVista.EsValido(Clave, pase, Ahora.AddMinutes(59)));
        Assert.False(PaseDeVista.EsValido(Clave, pase, Ahora.AddHours(1)));
    }

    [Fact]
    public void Firmado_con_otra_clave_no_vale()
    {
        var pase = PaseDeVista.Emitir("otra-clave-de-firma-de-al-menos-32-bytes", Usuario, Ahora.AddHours(1));

        Assert.False(PaseDeVista.EsValido(Clave, pase, Ahora));
    }

    [Fact]
    public void Alargar_la_caducidad_o_cambiar_el_usuario_rompe_la_firma()
    {
        var partes = PaseDeVista.Emitir(Clave, Usuario, Ahora.AddHours(1)).Split('.');

        var alargado = $"{partes[0]}.{Ahora.AddDays(30).ToUnixTimeSeconds()}.{partes[2]}";
        var deOtro = $"{Guid.NewGuid():N}.{partes[1]}.{partes[2]}";

        Assert.False(PaseDeVista.EsValido(Clave, alargado, Ahora));
        Assert.False(PaseDeVista.EsValido(Clave, deOtro, Ahora));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("basura")]
    [InlineData("a.b.c")]
    [InlineData("0192a000000070008000000000000001.99999999999.zz")]
    public void Lo_que_no_es_un_pase_no_vale(string? pase)
    {
        Assert.False(PaseDeVista.EsValido(Clave, pase, Ahora));
    }
}
