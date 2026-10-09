using Viariato.Modules.Casos.Endpoints;
using Xunit;

namespace Viariato.Modules.Casos.Tests;

public sealed class NombresParaZipTests
{
    [Theory]
    [InlineData("informe.csv", "informe.csv")]
    [InlineData("a/b\\c:d*e?f\"g<h>i|j.txt", "a_b_c_d_e_f_g_h_i_j.txt")]
    [InlineData("  con espacios .  ", "con espacios")]
    [InlineData("fin con punto...", "fin con punto")]
    [InlineData("", "sin nombre")]
    [InlineData("...", "sin nombre")]
    [InlineData("a\tb\u0001c", "a_b_c")]
    public void Sanear_QuitaLoQueNingunSistemaDeArchivosAcepta(string entrada, string esperado)
    {
        Assert.Equal(esperado, NombresParaZip.Sanear(entrada));
    }

    [Fact]
    public void Sanear_NoDejaSubirDeCarpeta()
    {
        var nombre = NombresParaZip.Sanear("../../Windows/system32/config");

        // One flat name: with no separator in it there is no way out of the folder, whatever it starts with.
        Assert.DoesNotContain('/', nombre);
        Assert.DoesNotContain('\\', nombre);
        Assert.NotEqual("..", nombre);
        Assert.Equal("_.._Windows_system32_config", nombre);
    }

    [Fact]
    public void Sanear_Null_EsSinNombre()
    {
        Assert.Equal("sin nombre", NombresParaZip.Sanear(null));
    }

    [Fact]
    public void Sanear_CortaLoLargo_ConservandoLaExtension()
    {
        var nombre = NombresParaZip.Sanear(new string('a', 300) + ".pdf", maximo: 50);

        Assert.Equal(50, nombre.Length);
        Assert.EndsWith(".pdf", nombre);
    }

    [Fact]
    public void Sanear_CortaLoLargoSinExtension()
    {
        Assert.Equal(40, NombresParaZip.Sanear(new string('b', 300), maximo: 40).Length);
    }

    [Fact]
    public void Unico_NumeraLosRepetidos_ConservandoLaExtension()
    {
        var usados = NombresParaZip.ConjuntoDeNombres();

        Assert.Equal("dni.pdf", NombresParaZip.Unico(usados, "dni.pdf"));
        Assert.Equal("dni (2).pdf", NombresParaZip.Unico(usados, "dni.pdf"));
        Assert.Equal("dni (3).pdf", NombresParaZip.Unico(usados, "dni.pdf"));
        Assert.Equal("notas", NombresParaZip.Unico(usados, "notas"));
        Assert.Equal("notas (2)", NombresParaZip.Unico(usados, "notas"));
    }

    [Fact]
    public void Unico_LasMayusculasNoLosDistinguen_PorqueWindowsTampoco()
    {
        var usados = NombresParaZip.ConjuntoDeNombres();

        NombresParaZip.Unico(usados, "Informe.CSV");

        Assert.Equal("informe (2).csv", NombresParaZip.Unico(usados, "informe.csv"));
    }

    [Fact]
    public void CarpetaDeCaso_LlevaElTituloYElInicioDelId_ParaQueDosCasosIgualesNoCompartan()
    {
        var a = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var b = Guid.Parse("99999999-2222-3333-4444-555555555555");

        Assert.Equal("Balay_ lavadoras (11111111)", NombresParaZip.CarpetaDeCaso("Balay/ lavadoras", a));
        Assert.NotEqual(NombresParaZip.CarpetaDeCaso("Mismo título", a), NombresParaZip.CarpetaDeCaso("Mismo título", b));
    }
}
