using Microsoft.Extensions.Options;
using Viariato.ApiContracts;
using Viriato.Rpa.Client;

namespace Viriato.Rpa.Client.Tests;

public sealed class VistaEnDirectoNoVncTests
{
    private static readonly VistaEnDirectoDto Baja = new("Baja", 500, 2, 9);
    private static readonly VistaEnDirectoDto Media = new("Media", 200, 5, 6);

    /// <summary>Records what would be started and stopped instead of running x11vnc.</summary>
    private sealed class LanzadorFalso : ILanzadorDeProcesos
    {
        public List<string> Registro { get; } = [];
        public bool Falla { get; set; }

        public IDisposable? Lanzar(string programa, IReadOnlyList<string> argumentos)
        {
            if (Falla) return null;
            Registro.Add($"inicia {programa} {string.Join(' ', argumentos)}");
            return new Detener(() => Registro.Add($"para {programa}"));
        }

        private sealed class Detener(Action alParar) : IDisposable
        {
            public void Dispose() => alParar();
        }
    }

    private static DespliegueEstadoDto Estado(PantallaDelRobotDto? pantalla) => new(true, "PC-01", "Extractor", "P01", pantalla);

    private static (VistaEnDirectoNoVnc Vista, LanzadorFalso Lanzador) Crear(VistaEnDirectoOptions? opciones = null)
    {
        var lanzador = new LanzadorFalso();
        var o = opciones ?? new VistaEnDirectoOptions { Pantalla = ":99" };
        return (new VistaEnDirectoNoVnc(Options.Create(o), lanzador), lanzador);
    }

    [Fact]
    public async Task TheRateViriatoDecides_IsWhatX11vncIsStartedWith()
    {
        var (vista, lanzador) = Crear();

        await vista.AplicarAsync(Estado(new(Baja, null)), default);

        Assert.Equal(2, lanzador.Registro.Count);
        Assert.Contains("-wait 500 -defer 500", lanzador.Registro[0]);
        Assert.Contains("-viewonly", lanzador.Registro[0]);
        Assert.Contains("-nopw", lanzador.Registro[0]);
        Assert.StartsWith("inicia websockify --web /usr/share/novnc 6080 localhost:5900", lanzador.Registro[1]);
        Assert.Equal(Baja, vista.Aplicada);
    }

    [Fact]
    public async Task TheSameSettingAgain_TouchesNothing()
    {
        var (vista, lanzador) = Crear();

        await vista.AplicarAsync(Estado(new(Baja, null)), default);
        await vista.AplicarAsync(Estado(new(Baja with { }, true)), default);

        Assert.Equal(2, lanzador.Registro.Count);
    }

    [Fact]
    public async Task ANewQuality_RestartsOnlyX11vnc()
    {
        var (vista, lanzador) = Crear();
        await vista.AplicarAsync(Estado(new(Baja, null)), default);

        await vista.AplicarAsync(Estado(new(Media, null)), default);

        Assert.Equal(["para x11vnc"], lanzador.Registro.Skip(2).Take(1));
        Assert.Contains("-wait 200 -defer 200", lanzador.Registro[3]);
        Assert.Equal(4, lanzador.Registro.Count);
    }

    [Fact]
    public async Task SwitchedOffInViriato_StopsEverything()
    {
        var (vista, lanzador) = Crear();
        await vista.AplicarAsync(Estado(new(Baja, null)), default);

        await vista.AplicarAsync(Estado(new(null, null)), default);

        Assert.Contains("para x11vnc", lanzador.Registro);
        Assert.Contains("para websockify", lanzador.Registro);
        Assert.Null(vista.Aplicada);
    }

    [Fact]
    public async Task OffFromTheStart_StartsNothing()
    {
        var (vista, lanzador) = Crear();

        await vista.AplicarAsync(Estado(new(null, false)), default);

        Assert.Empty(lanzador.Registro);
    }

    [Fact]
    public async Task AViriatoThatSaysNothing_GetsTheViewAsItWasBefore()
    {
        var (vista, lanzador) = Crear();

        await vista.AplicarAsync(Estado(null), default);

        Assert.Equal(VistaEnDirectoNoVnc.SinDecisionDeViriato, vista.Aplicada);
        Assert.Contains("-wait 20 -defer 20", lanzador.Registro[0]);
    }

    [Fact]
    public async Task TheRobotsVeto_WinsOverViriato()
    {
        var (vetada, lanzador) = Crear(new VistaEnDirectoOptions { Pantalla = ":99", Permitida = false });
        await vetada.AplicarAsync(Estado(new(Media, null)), default);

        Assert.Empty(lanzador.Registro);
        Assert.Null(vetada.Aplicada);
    }

    [Fact]
    public async Task APassword_IsHandedOverInAFileThatX11vncDeletes_AndControlCanBeAllowed()
    {
        var (vista, lanzador) = Crear(new VistaEnDirectoOptions { Pantalla = ":99", Contrasena = "secreta", PermitirControl = true });

        await vista.AplicarAsync(Estado(new(Baja, null)), default);

        Assert.DoesNotContain("secreta", lanzador.Registro[0]);
        Assert.Contains("-passwdfile rm:", lanzador.Registro[0]);
        Assert.DoesNotContain("-viewonly", lanzador.Registro[0]);
        Assert.DoesNotContain("-nopw", lanzador.Registro[0]);
    }

    [Fact]
    public async Task WhenX11vncIsMissing_ItDoesNotKeepRetrying()
    {
        var (vista, lanzador) = Crear();
        lanzador.Falla = true;

        await vista.AplicarAsync(Estado(new(Baja, null)), default);
        lanzador.Falla = false;
        await vista.AplicarAsync(Estado(new(Baja, null)), default);

        Assert.Empty(lanzador.Registro);
    }

    [Fact]
    public async Task Disposing_StopsWhatIsRunning()
    {
        var (vista, lanzador) = Crear();
        await vista.AplicarAsync(Estado(new(Baja, null)), default);

        vista.Dispose();

        Assert.Contains("para x11vnc", lanzador.Registro);
        Assert.Contains("para websockify", lanzador.Registro);
    }
}
