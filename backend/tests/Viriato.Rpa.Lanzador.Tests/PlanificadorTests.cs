using Viariato.ApiContracts;
using Viriato.Rpa.Lanzador;

namespace Viriato.Rpa.Lanzador.Tests;

public sealed class PlanificadorTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 11, 10, 0, 0, TimeSpan.Zero);

    private static RobotOptions Robot(string nombre, int maxCopias = 1, bool pantalla = false) =>
        new() { Nombre = nombre, ClaveEnv = nombre.ToUpperInvariant() + "_KEY", Comando = "dotnet", MaxCopias = maxCopias, Pantalla = pantalla };

    private static TrabajoDelDespliegueDto Trabajo(int pendientes = 0, int enEjecucion = 0, bool encendido = true, bool alLimite = false) =>
        new(encendido, pendientes, enEjecucion, alLimite);

    private static LanzadorOptions Opciones(int maxRobots = 2, int maxConPantalla = 1) =>
        new() { ViriatoUrl = "http://viriato.test", MaxRobotsALaVez = maxRobots, MaxConPantalla = maxConPantalla };

    private static DecisionDeRobot Una(LanzadorOptions o, SituacionDeRobot situacion) => Planificador.Decidir(o, [situacion], Ahora).Single();

    [Fact]
    public void WithNothingWaiting_NothingIsStarted_AndThatIsReportedAsNoWork()
    {
        var decision = Una(Opciones(), new(Robot("extractor"), Trabajo(), Vivas: 0));

        Assert.Equal(0, decision.Arrancar);
        Assert.Equal(EstadosDelLanzador.SinTrabajo, decision.Estado);
        Assert.Null(decision.Detalle);
    }

    [Fact]
    public void WithWorkWaitingAndNoCopy_OneIsStarted()
    {
        var decision = Una(Opciones(), new(Robot("extractor"), Trabajo(pendientes: 7), Vivas: 0));

        Assert.Equal(1, decision.Arrancar);
        Assert.Equal(EstadosDelLanzador.EnMarcha, decision.Estado);
    }

    [Fact]
    public void ARobotIsGivenOneCopyPerStep_UpToItsOwnMaximum()
    {
        var decision = Una(Opciones(maxRobots: 10), new(Robot("extractor", maxCopias: 3), Trabajo(pendientes: 7), Vivas: 0));

        Assert.Equal(3, decision.Arrancar);
    }

    [Fact]
    public void AnIdleCopyAlreadyRunning_TakesTheWork_SoNothingNewIsStarted()
    {
        // One copy is there and holds nothing: it will be handed the step that waits.
        var decision = Una(Opciones(maxRobots: 10), new(Robot("extractor", maxCopias: 3), Trabajo(pendientes: 1, enEjecucion: 0), Vivas: 1));

        Assert.Equal(0, decision.Arrancar);
        Assert.Equal(EstadosDelLanzador.EnMarcha, decision.Estado);
    }

    [Fact]
    public void ABusyCopy_DoesNotCountForWhatWaits_SoMoreAreStarted()
    {
        var decision = Una(Opciones(maxRobots: 10), new(Robot("extractor", maxCopias: 3), Trabajo(pendientes: 5, enEjecucion: 1), Vivas: 1));

        Assert.Equal(2, decision.Arrancar);
    }

    [Fact]
    public void ARunningCopyIsNeverStoppedHere_EvenWithNothingLeftToDo()
    {
        var decision = Una(Opciones(), new(Robot("extractor"), Trabajo(), Vivas: 1));

        Assert.Equal(0, decision.Arrancar);
        Assert.Equal(EstadosDelLanzador.EnMarcha, decision.Estado);
    }

    [Fact]
    public void ASwitchedOffDespliegue_StartsNothing_AndSaysSo()
    {
        var decision = Una(Opciones(), new(Robot("extractor"), Trabajo(pendientes: 4, encendido: false), Vivas: 0));

        Assert.Equal(0, decision.Arrancar);
        Assert.Equal(EstadosDelLanzador.Apagado, decision.Estado);
    }

    [Fact]
    public void AServiceAtItsGlobalCap_StartsNothing_AndWaitsSayingWhy()
    {
        var decision = Una(Opciones(), new(Robot("extractor"), Trabajo(pendientes: 4, alLimite: true), Vivas: 0));

        Assert.Equal(0, decision.Arrancar);
        Assert.Equal(EstadosDelLanzador.EnEspera, decision.Estado);
        Assert.Contains("límite global", decision.Detalle);
    }

    [Fact]
    public void TheMachineCeiling_IsSharedByAllRobots_InTheCataloguesOrder()
    {
        var decisiones = Planificador.Decidir(
            Opciones(maxRobots: 2),
            [
                new(Robot("uno", maxCopias: 2), Trabajo(pendientes: 5), Vivas: 0),
                new(Robot("dos"), Trabajo(pendientes: 5), Vivas: 0),
            ],
            Ahora);

        Assert.Equal(2, decisiones[0].Arrancar);
        Assert.Equal(0, decisiones[1].Arrancar);
        Assert.Equal(EstadosDelLanzador.EnEspera, decisiones[1].Estado);
        Assert.Contains("2 de 2 robots", decisiones[1].Detalle);
    }

    [Fact]
    public void WhatIsAlreadyRunning_CountsTowardsTheCeiling()
    {
        var decisiones = Planificador.Decidir(
            Opciones(maxRobots: 2),
            [
                new(Robot("uno"), Trabajo(enEjecucion: 1), Vivas: 1),
                new(Robot("dos", maxCopias: 3), Trabajo(pendientes: 5), Vivas: 0),
            ],
            Ahora);

        Assert.Equal(1, decisiones[1].Arrancar);
        Assert.Equal(EstadosDelLanzador.EnMarcha, decisiones[1].Estado);
        // It runs, and it also says why it does not run more.
        Assert.Contains("Esperando hueco", decisiones[1].Detalle);
    }

    [Fact]
    public void RobotsWithAScreen_HaveALowerCeilingOfTheirOwn()
    {
        var decisiones = Planificador.Decidir(
            Opciones(maxRobots: 5, maxConPantalla: 1),
            [
                new(Robot("navegador-a", pantalla: true), Trabajo(pendientes: 1), Vivas: 0),
                new(Robot("navegador-b", pantalla: true), Trabajo(pendientes: 1), Vivas: 0),
                new(Robot("ligero"), Trabajo(pendientes: 1), Vivas: 0),
            ],
            Ahora);

        Assert.Equal(1, decisiones[0].Arrancar);
        Assert.Equal(0, decisiones[1].Arrancar);
        Assert.Equal(EstadosDelLanzador.EnEspera, decisiones[1].Estado);
        Assert.Contains("navegador", decisiones[1].Detalle);
        // The light one is not held back by the screens.
        Assert.Equal(1, decisiones[2].Arrancar);
    }

    [Fact]
    public void ALauncherWithoutScreens_SaysItCannotRunABrowserRobot()
    {
        var decision = Una(Opciones(maxConPantalla: 0), new(Robot("navegador", pantalla: true), Trabajo(pendientes: 1), Vivas: 0));

        Assert.Equal(0, decision.Arrancar);
        Assert.Contains("no admite robots con navegador", decision.Detalle);
    }

    [Fact]
    public void ARobotThatJustFailed_IsNotStartedAgainUntilItsWaitIsOver()
    {
        var robot = Robot("extractor");
        var fallo = "El robot terminó con un error (código 1); mira su registro.";

        var esperando = Una(Opciones(), new(robot, Trabajo(pendientes: 1), Vivas: 0, FalloHasta: Ahora.AddSeconds(30), UltimoFallo: fallo));
        var despues = Una(Opciones(), new(robot, Trabajo(pendientes: 1), Vivas: 0, FalloHasta: Ahora.AddSeconds(-1), UltimoFallo: fallo));

        Assert.Equal(0, esperando.Arrancar);
        Assert.Equal(EstadosDelLanzador.Fallo, esperando.Estado);
        Assert.Contains("código 1", esperando.Detalle);
        Assert.Contains("Se reintenta", esperando.Detalle);
        Assert.Equal(1, despues.Arrancar);
    }

    [Fact]
    public void ARobotThatLeftWithoutBeingGivenWork_WaitsToo_ButIsReportedAsWaiting_NotAsBroken()
    {
        var decision = Una(Opciones(), new(
            Robot("extractor"), Trabajo(pendientes: 1), Vivas: 0, FalloHasta: Ahora.AddSeconds(30),
            UltimoFallo: "Arrancó, Viriato no le dio trabajo (¿no era su turno?) y se apagó.", FalloEsEspera: true));

        Assert.Equal(0, decision.Arrancar);
        Assert.Equal(EstadosDelLanzador.EnEspera, decision.Estado);
        Assert.Contains("no le dio trabajo", decision.Detalle);
    }

    [Fact]
    public void ARecentFailureWithNothingWaiting_IsNotAFailureAnyMore_ThereIsJustNoWork()
    {
        var decision = Una(Opciones(), new(Robot("extractor"), Trabajo(), Vivas: 0, FalloHasta: Ahora.AddSeconds(30), UltimoFallo: "x"));

        Assert.Equal(EstadosDelLanzador.SinTrabajo, decision.Estado);
    }

    [Fact]
    public void ARobotParkedWithZeroCopies_IsNeverStarted_AndSaysWhy()
    {
        var decision = Una(Opciones(), new(Robot("extractor", maxCopias: 0), Trabajo(pendientes: 3), Vivas: 0));

        Assert.Equal(0, decision.Arrancar);
        Assert.Equal(EstadosDelLanzador.EnEspera, decision.Estado);
        Assert.Contains("aparcado", decision.Detalle);
    }

    [Fact]
    public void WhenViriatoCouldNotBeAsked_NothingIsDecidedNorReported_ButItsCopiesStillCount()
    {
        var decisiones = Planificador.Decidir(
            Opciones(maxRobots: 1),
            [
                new(Robot("sin-respuesta"), Trabajo: null, Vivas: 1),
                new(Robot("otro"), Trabajo(pendientes: 1), Vivas: 0),
            ],
            Ahora);

        Assert.Equal(0, decisiones[0].Arrancar);
        Assert.Null(decisiones[0].Estado);
        Assert.Equal(0, decisiones[1].Arrancar);
        Assert.Equal(EstadosDelLanzador.EnEspera, decisiones[1].Estado);
    }

    [Theory]
    [InlineData(1, 60)]
    [InlineData(2, 120)]
    [InlineData(3, 240)]
    [InlineData(10, 900)]
    [InlineData(500, 900)]
    public void TheWaitAfterAFailure_DoublesEachTime_UpToFifteenMinutes(int fallosSeguidos, int segundos)
    {
        Assert.Equal(TimeSpan.FromSeconds(segundos), Planificador.EsperaTrasFallo(new LanzadorOptions { EsperaTrasFalloSegundos = 60 }, fallosSeguidos));
    }
}

public sealed class CatalogoTests
{
    private static LanzadorOptions Valido() => new()
    {
        ViriatoUrl = "http://viriato.test",
        Robots = [new RobotOptions { Nombre = "extractor", ClaveEnv = "EXTRACTOR_API_KEY", Comando = "dotnet" }],
    };

    private static string? Variables(string nombre) => nombre == "EXTRACTOR_API_KEY" ? "clave" : null;

    [Fact]
    public void ACompleteCatalogue_HasNoProblems() => Assert.Empty(Valido().Validar(Variables));

    [Fact]
    public void AnEmptyCatalogue_NamesEverythingMissing()
    {
        var problemas = new LanzadorOptions().Validar(Variables);

        Assert.Contains(problemas, p => p.Contains("VIRIATO_URL"));
        Assert.Contains(problemas, p => p.Contains("ningún robot"));
    }

    [Fact]
    public void ARobotWhoseKeyVariableIsEmpty_IsReportedNamingTheVariable_NeverAValue()
    {
        var problemas = Valido().Validar(_ => null);

        Assert.Contains(problemas, p => p.Contains("EXTRACTOR_API_KEY") && p.Contains("extractor"));
    }

    [Fact]
    public void EveryMistakeInARobot_IsListedAtOnce()
    {
        var o = Valido();
        o.Robots.Add(new RobotOptions { Nombre = "EXTRACTOR", ClaveEnv = "", Comando = "", MaxCopias = -1, MemoriaMaximaMb = 0 });

        var problemas = o.Validar(Variables);

        Assert.Contains(problemas, p => p.Contains("dos robots"));
        Assert.Contains(problemas, p => p.Contains("\"comando\""));
        Assert.Contains(problemas, p => p.Contains("\"claveEnv\""));
        Assert.Contains(problemas, p => p.Contains("\"maxCopias\""));
        Assert.Contains(problemas, p => p.Contains("\"memoriaMaximaMb\""));
    }

    [Theory]
    [InlineData("viriato.test")]
    [InlineData("ftp://viriato.test")]
    public void AnAddressThatIsNotHttp_IsReported(string url)
    {
        var o = Valido();
        o.ViriatoUrl = url;

        Assert.Contains(o.Validar(Variables), p => p.Contains("no es una URL http(s) válida"));
    }
}

public sealed class EntornoDelRobotTests
{
    private static LanzadorOptions Opciones() => new() { ViriatoUrl = "http://viriato.test", SalirSinTrabajoSegundos = 45 };

    [Fact]
    public void ARobotIsTold_WhereViriatoIs_ItsKey_WhenToLeave_AndItsOwnTemporaryFolder()
    {
        var robot = new RobotOptions { Nombre = "creador" };

        var entorno = EntornoDelRobot.Construir(new ArranqueDeRobot(robot, "la-clave", null, Opciones()), "/tmp/lanzador/creador-1", _ => null);

        Assert.Equal("http://viriato.test", entorno["Viriato__BaseUrl"]);
        Assert.Equal("la-clave", entorno["Viriato__ApiKey"]);
        Assert.Equal("00:00:45", entorno["Worker__SalirSinTrabajo"]);
        Assert.Equal("/tmp/lanzador/creador-1", entorno["TMPDIR"]);
        Assert.False(entorno.ContainsKey("DISPLAY"));
    }

    [Fact]
    public void EachScreenSlot_HasItsOwnDisplayAndLiveViewPorts_SoTwoBrowsersNeverShare()
    {
        var robot = new RobotOptions { Nombre = "extractor", Pantalla = true, Entorno = { ["Vista__UrlPublica"] = "http://localhost:{puertoWeb}/vnc.html" } };

        var primero = EntornoDelRobot.Construir(new ArranqueDeRobot(robot, "k", 0, Opciones()), "/t", _ => null);
        var segundo = EntornoDelRobot.Construir(new ArranqueDeRobot(robot, "k", 1, Opciones()), "/t", _ => null);

        Assert.Equal(":99", primero["DISPLAY"]);
        Assert.Equal("6080", primero["VistaEnDirecto__PuertoWeb"]);
        Assert.Equal("5900", primero["VistaEnDirecto__PuertoVnc"]);
        Assert.Equal("http://localhost:6080/vnc.html", primero["Vista__UrlPublica"]);

        Assert.Equal(":100", segundo["DISPLAY"]);
        Assert.Equal("6081", segundo["VistaEnDirecto__PuertoWeb"]);
        Assert.Equal("5901", segundo["VistaEnDirecto__PuertoVnc"]);
        Assert.Equal("http://localhost:6081/vnc.html", segundo["Vista__UrlPublica"]);
    }

    [Fact]
    public void ARobotsOwnSettings_CanTakeValuesFromTheLaunchersEnvironment()
    {
        var robot = new RobotOptions { Nombre = "extractor", Entorno = { ["Tradeplace__Credencial"] = "${TRADEPLACE_CREDENCIAL}", ["Otro"] = "a-${NO_EXISTE}-b" } };

        var entorno = EntornoDelRobot.Construir(
            new ArranqueDeRobot(robot, "k", null, Opciones()), "/t", n => n == "TRADEPLACE_CREDENCIAL" ? "tradeplace_credentials" : null);

        Assert.Equal("tradeplace_credentials", entorno["Tradeplace__Credencial"]);
        Assert.Equal("a--b", entorno["Otro"]);
    }
}
