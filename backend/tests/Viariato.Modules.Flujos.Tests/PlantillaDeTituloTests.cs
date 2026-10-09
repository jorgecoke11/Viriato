using Viariato.Modules.Flujos.Esquemas;
using Xunit;

namespace Viariato.Modules.Flujos.Tests;

public sealed class PlantillaDeTituloTests
{
    private static readonly IReadOnlyDictionary<string, string> Valores = new Dictionary<string, string>
    {
        ["creador"] = "Alta de cliente",
        ["proceso"] = "Altas",
        ["tipo"] = "Urgente",
        ["fecha"] = "2026-10-08",
        ["hora"] = "14:30",
        ["n"] = "7",
    };

    [Fact]
    public void LaPredeterminada_UsaElCreadorLaFechaYElNumero()
    {
        Assert.Equal("Alta de cliente 2026-10-08 #7", PlantillaDeTitulo.Renderizar(PlantillaDeTitulo.Predeterminada, Valores, null));
    }

    [Fact]
    public void LosCamposDeLosDatos_EntranEnElTituloYBajanPorLosObjetos()
    {
        var datos = """{ "cliente": { "nombre": "ACME", "edad": 40 }, "urgente": true }""";

        var titulo = PlantillaDeTitulo.Renderizar("{tipo}: {datos.cliente.nombre} ({datos.cliente.edad}) {datos.urgente}", Valores, datos);

        Assert.Equal("Urgente: ACME (40) true", titulo);
    }

    [Fact]
    public void UnCampoQueNoEsta_DesapareceSinDejarHuecos()
    {
        var titulo = PlantillaDeTitulo.Renderizar("{creador} - {datos.cliente} - #{n}", Valores, """{ "otro": 1 }""");

        Assert.Equal("Alta de cliente - - #7", titulo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("esto no es json")]
    public void SinDatosUtiles_LosMarcadoresDeDatosSeVacian_YNuncaFalla(string? datos)
    {
        Assert.Equal("Alta de cliente", PlantillaDeTitulo.Renderizar("{creador} {datos.cliente}", Valores, datos));
    }

    [Fact]
    public void UnCampoQueEsUnObjetoOUnaLista_NoSeVuelcaEnElTitulo()
    {
        Assert.Equal("x", PlantillaDeTitulo.Renderizar("x{datos.a}{datos.b}", Valores, """{ "a": { "z": 1 }, "b": [1, 2] }"""));
    }

    [Fact]
    public void ElTitulo_NuncaSuperaElMaximo()
    {
        var titulo = PlantillaDeTitulo.Renderizar(new string('a', 250) + "{datos.largo}", Valores, $$"""{ "largo": "{{new string('b', 200)}}" }""");

        Assert.Equal(PlantillaDeTitulo.LongitudMaxima, titulo.Length);
    }

    [Fact]
    public void Desconocidos_AvisaDeLasErratasPeroNoDeLosCamposDeDatos()
    {
        var desconocidos = PlantillaDeTitulo.Desconocidos("{creador} {fehca} {datos.x} {datos.} {n} {fehca}");

        Assert.Equal(["fehca", "datos."], desconocidos);
    }
}
