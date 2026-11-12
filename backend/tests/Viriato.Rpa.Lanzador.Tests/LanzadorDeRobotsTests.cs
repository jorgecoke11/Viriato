using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Viariato.ApiContracts;
using Viriato.Rpa.Lanzador;

namespace Viriato.Rpa.Lanzador.Tests;

public sealed class LanzadorDeRobotsTests
{
    private sealed class OpcionesFijas(LanzadorOptions valor) : IOptionsMonitor<LanzadorOptions>
    {
        public LanzadorOptions CurrentValue => valor;
        public LanzadorOptions Get(string? name) => valor;
        public IDisposable? OnChange(Action<LanzadorOptions, string?> listener) => null;
    }

    /// <summary>Viriato as the launcher sees it: what each key's robot has, and what was reported about it.</summary>
    private sealed class ViriatoFalso : IViriatoDelLanzador
    {
        public Dictionary<string, TrabajoDelDespliegueDto> Trabajo { get; } = [];
        public Dictionary<string, Exception> Fallos { get; } = [];
        public Dictionary<string, EstadoDelLanzadorRequest> Reportado { get; } = [];

        public Task<TrabajoDelDespliegueDto> TrabajoAsync(string clave, CancellationToken ct) =>
            Fallos.TryGetValue(clave, out var fallo) ? Task.FromException<TrabajoDelDespliegueDto>(fallo) : Task.FromResult(Trabajo[clave]);

        public Task ReportarAsync(string clave, EstadoDelLanzadorRequest estado, CancellationToken ct)
        {
            Reportado[clave] = estado;
            return Task.CompletedTask;
        }
    }

    private sealed class CopiaFalsa(ArranqueDeRobot arranque) : IRobotEnMarcha
    {
        public ArranqueDeRobot Arranque { get; } = arranque;
        public bool Vivo { get; private set; } = true;
        public int? CodigoDeSalida { get; private set; }
        public int? HuecoDePantalla => Arranque.HuecoDePantalla;
        public long? Memoria { get; set; }
        public bool Parada { get; private set; }
        public bool Limpia { get; private set; }

        public long? MemoriaMb() => Memoria;

        public void Terminar(int codigo)
        {
            Vivo = false;
            CodigoDeSalida = codigo;
        }

        public Task PararAsync(TimeSpan gracia)
        {
            Parada = true;
            Vivo = false;
            Limpia = true;
            return Task.CompletedTask;
        }

        public void Limpiar() => Limpia = true;
    }

    private sealed class ArrancadorFalso : IArrancadorDeRobots
    {
        public List<CopiaFalsa> Arrancadas { get; } = [];
        public string? NoArranca { get; set; }

        public IRobotEnMarcha Arrancar(ArranqueDeRobot arranque)
        {
            if (NoArranca is not null) throw new InvalidOperationException(NoArranca);
            var copia = new CopiaFalsa(arranque);
            Arrancadas.Add(copia);
            return copia;
        }
    }

    private sealed record Montaje(
        LanzadorDeRobots Lanzador, ViriatoFalso Viriato, ArrancadorFalso Arrancador, FakeTimeProvider Reloj, LanzadorOptions Opciones);

    private static RobotOptions Robot(string nombre, int maxCopias = 1, bool pantalla = false, int? memoria = null) =>
        new() { Nombre = nombre, ClaveEnv = "CLAVE_" + nombre, Comando = "dotnet", MaxCopias = maxCopias, Pantalla = pantalla, MemoriaMaximaMb = memoria };

    private static TrabajoDelDespliegueDto Trabajo(int pendientes = 0, int enEjecucion = 0, bool encendido = true) => new(encendido, pendientes, enEjecucion, false);

    private static Montaje Montar(Action<LanzadorOptions>? ajustar = null, params RobotOptions[] robots)
    {
        var opciones = new LanzadorOptions { ViriatoUrl = "http://viriato.test", MaxRobotsALaVez = 4, MaxConPantalla = 2, Robots = [.. robots] };
        ajustar?.Invoke(opciones);
        var viriato = new ViriatoFalso();
        foreach (var robot in robots) viriato.Trabajo["k-" + robot.Nombre] = Trabajo();
        var arrancador = new ArrancadorFalso();
        var reloj = new FakeTimeProvider(new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero));
        // Each robot's key is in its own variable; the launcher never sees it anywhere else.
        string? Variable(string nombre) => nombre.StartsWith("CLAVE_", StringComparison.Ordinal) ? "k-" + nombre["CLAVE_".Length..] : null;

        var lanzador = new LanzadorDeRobots(new OpcionesFijas(opciones), viriato, arrancador, Variable, reloj, NullLogger<LanzadorDeRobots>.Instance);
        return new Montaje(lanzador, viriato, arrancador, reloj, opciones);
    }

    [Fact]
    public async Task WithNoWork_NothingRuns_AndViriatoIsToldSo()
    {
        var m = Montar(null, Robot("extractor"));

        await m.Lanzador.CicloAsync(default);

        Assert.Empty(m.Arrancador.Arrancadas);
        Assert.Equal(new EstadoDelLanzadorRequest(EstadosDelLanzador.SinTrabajo, 0, 1), m.Viriato.Reportado["k-extractor"]);
    }

    [Fact]
    public async Task WhenWorkArrives_TheRobotIsStartedWithItsOwnKey_AndOnlyOnce()
    {
        var m = Montar(null, Robot("extractor"), Robot("creador"));
        m.Viriato.Trabajo["k-extractor"] = Trabajo(pendientes: 3);

        await m.Lanzador.CicloAsync(default);
        // Next round the copy is there and idle (it has not been handed the step yet): no second one.
        await m.Lanzador.CicloAsync(default);

        var copia = Assert.Single(m.Arrancador.Arrancadas);
        Assert.Equal("extractor", copia.Arranque.Robot.Nombre);
        Assert.Equal("k-extractor", copia.Arranque.Clave);
        Assert.Equal(EstadosDelLanzador.EnMarcha, m.Viriato.Reportado["k-extractor"].Estado);
        Assert.Equal(1, m.Viriato.Reportado["k-extractor"].Copias);
        Assert.Equal(EstadosDelLanzador.SinTrabajo, m.Viriato.Reportado["k-creador"].Estado);
    }

    [Fact]
    public async Task ACopyThatLeavesByItself_IsCleanedUp_AndTheRobotIsBackToNoWork()
    {
        var m = Montar(null, Robot("extractor"));
        m.Viriato.Trabajo["k-extractor"] = Trabajo(pendientes: 1);
        await m.Lanzador.CicloAsync(default);

        m.Arrancador.Arrancadas[0].Terminar(0);
        m.Viriato.Trabajo["k-extractor"] = Trabajo();
        await m.Lanzador.CicloAsync(default);

        Assert.True(m.Arrancador.Arrancadas[0].Limpia);
        Assert.Equal(0, m.Lanzador.EnMarcha["extractor"]);
        Assert.Equal(EstadosDelLanzador.SinTrabajo, m.Viriato.Reportado["k-extractor"].Estado);
    }

    [Fact]
    public async Task ACopyThatLeavesWithoutHavingBeenGivenTheWaitingWork_IsNotStartedAgainAtOnce()
    {
        var m = Montar(o => o.EsperaTrasFalloSegundos = 60, Robot("extractor"));
        m.Viriato.Trabajo["k-extractor"] = Trabajo(pendientes: 2);
        await m.Lanzador.CicloAsync(default);

        // It ends well, but the queue is as long as it was and nothing was ever seen running: Viriato gave it nothing.
        m.Arrancador.Arrancadas[0].Terminar(0);
        await m.Lanzador.CicloAsync(default);

        Assert.Single(m.Arrancador.Arrancadas);
        var reportado = m.Viriato.Reportado["k-extractor"];
        Assert.Equal(EstadosDelLanzador.EnEspera, reportado.Estado);
        Assert.Contains("no le dio trabajo", reportado.Detalle);

        m.Reloj.Advance(TimeSpan.FromSeconds(61));
        await m.Lanzador.CicloAsync(default);
        Assert.Equal(2, m.Arrancador.Arrancadas.Count);
    }

    [Fact]
    public async Task ACopyThatWorkedAndLeft_IsStartedAgainAtOnceWhenMoreWorkArrives()
    {
        var m = Montar(null, Robot("extractor"));
        m.Viriato.Trabajo["k-extractor"] = Trabajo(pendientes: 2);
        await m.Lanzador.CicloAsync(default);

        // Seen working, then the queue empties and it leaves...
        m.Viriato.Trabajo["k-extractor"] = Trabajo(pendientes: 1, enEjecucion: 1);
        await m.Lanzador.CicloAsync(default);
        m.Arrancador.Arrancadas[0].Terminar(0);

        // ...and new work is already there when the launcher notices.
        m.Viriato.Trabajo["k-extractor"] = Trabajo(pendientes: 3);
        await m.Lanzador.CicloAsync(default);

        Assert.Equal(2, m.Arrancador.Arrancadas.Count);
        Assert.Equal(EstadosDelLanzador.EnMarcha, m.Viriato.Reportado["k-extractor"].Estado);
    }

    [Fact]
    public async Task ACopyThatDidQuickWorkBetweenTwoLooks_IsNotMistakenForOneThatDidNothing()
    {
        var m = Montar(null, Robot("extractor"));
        m.Viriato.Trabajo["k-extractor"] = Trabajo(pendientes: 3);
        await m.Lanzador.CicloAsync(default);

        // Never caught in the act, but the queue is shorter than when it started.
        m.Arrancador.Arrancadas[0].Terminar(0);
        m.Viriato.Trabajo["k-extractor"] = Trabajo(pendientes: 1);
        await m.Lanzador.CicloAsync(default);

        Assert.Equal(2, m.Arrancador.Arrancadas.Count);
    }

    [Fact]
    public async Task ACopyThatDiesWithAnError_IsNotRestartedRightAway_AndViriatoIsToldWhy()
    {
        var m = Montar(o => o.EsperaTrasFalloSegundos = 60, Robot("extractor"));
        m.Viriato.Trabajo["k-extractor"] = Trabajo(pendientes: 1);
        await m.Lanzador.CicloAsync(default);

        m.Arrancador.Arrancadas[0].Terminar(1);
        await m.Lanzador.CicloAsync(default);

        Assert.Single(m.Arrancador.Arrancadas);
        var reportado = m.Viriato.Reportado["k-extractor"];
        Assert.Equal(EstadosDelLanzador.Fallo, reportado.Estado);
        Assert.Contains("código 1", reportado.Detalle);

        // Once the wait is over it is tried again.
        m.Reloj.Advance(TimeSpan.FromSeconds(61));
        await m.Lanzador.CicloAsync(default);

        Assert.Equal(2, m.Arrancador.Arrancadas.Count);
        Assert.Equal(EstadosDelLanzador.EnMarcha, m.Viriato.Reportado["k-extractor"].Estado);
    }

    [Fact]
    public async Task ARobotThatCannotBeStarted_IsReportedAsFailedWithTheReason()
    {
        var m = Montar(null, Robot("extractor"));
        m.Viriato.Trabajo["k-extractor"] = Trabajo(pendientes: 1);
        m.Arrancador.NoArranca = "No se pudo ejecutar \"dotnet\": no existe.";

        await m.Lanzador.CicloAsync(default);

        var reportado = m.Viriato.Reportado["k-extractor"];
        Assert.Equal(EstadosDelLanzador.Fallo, reportado.Estado);
        Assert.Contains("No se pudo arrancar", reportado.Detalle);
        Assert.Contains("no existe", reportado.Detalle);
    }

    [Fact]
    public async Task TwoBrowserRobots_GetDifferentScreenSlots_AndAFreedSlotIsReused()
    {
        var m = Montar(null, Robot("a", pantalla: true), Robot("b", pantalla: true));
        m.Viriato.Trabajo["k-a"] = Trabajo(pendientes: 1);
        m.Viriato.Trabajo["k-b"] = Trabajo(pendientes: 1);

        await m.Lanzador.CicloAsync(default);

        Assert.Equal([0, 1], m.Arrancador.Arrancadas.Select(c => c.HuecoDePantalla));

        // "a" finishes; when it has work again it gets the slot it left, not a third one.
        m.Arrancador.Arrancadas[0].Terminar(0);
        m.Viriato.Trabajo["k-a"] = Trabajo();
        m.Viriato.Trabajo["k-b"] = Trabajo(enEjecucion: 1);
        await m.Lanzador.CicloAsync(default);
        m.Viriato.Trabajo["k-a"] = Trabajo(pendientes: 1);
        await m.Lanzador.CicloAsync(default);

        Assert.Equal(3, m.Arrancador.Arrancadas.Count);
        Assert.Equal(0, m.Arrancador.Arrancadas[2].HuecoDePantalla);
    }

    [Fact]
    public async Task ACopyOverItsMemoryLimit_IsStopped_AndCountedAsAFailure()
    {
        var m = Montar(null, Robot("extractor", memoria: 1500));
        m.Viriato.Trabajo["k-extractor"] = Trabajo(pendientes: 1);
        await m.Lanzador.CicloAsync(default);

        m.Arrancador.Arrancadas[0].Memoria = 1800;
        await m.Lanzador.CicloAsync(default);

        Assert.True(m.Arrancador.Arrancadas[0].Parada);
        var reportado = m.Viriato.Reportado["k-extractor"];
        Assert.Equal(EstadosDelLanzador.Fallo, reportado.Estado);
        Assert.Contains("1800 MB", reportado.Detalle);
        Assert.Contains("1500 MB", reportado.Detalle);
    }

    [Fact]
    public async Task ACopyThatShouldHaveLeftAndHasNot_IsStoppedAfterItsGrace()
    {
        var m = Montar(o => { o.SalirSinTrabajoSegundos = 30; o.GraciaParaSalirSegundos = 60; }, Robot("antiguo"));
        m.Viriato.Trabajo["k-antiguo"] = Trabajo(pendientes: 1);
        await m.Lanzador.CicloAsync(default);

        // The queue is empty and the robot holds nothing, yet the copy stays (a client older than 0.12, or a hung one).
        m.Viriato.Trabajo["k-antiguo"] = Trabajo();
        await m.Lanzador.CicloAsync(default);
        m.Reloj.Advance(TimeSpan.FromSeconds(80));
        await m.Lanzador.CicloAsync(default);
        Assert.False(m.Arrancador.Arrancadas[0].Parada);

        m.Reloj.Advance(TimeSpan.FromSeconds(15));
        await m.Lanzador.CicloAsync(default);

        Assert.True(m.Arrancador.Arrancadas[0].Parada);
        Assert.Equal(EstadosDelLanzador.SinTrabajo, m.Viriato.Reportado["k-antiguo"].Estado);
    }

    [Fact]
    public async Task ACopyThatIsThereButDoesNotTakeTheWaitingWork_IsRestarted()
    {
        var m = Montar(o => o.ReiniciarSiNoCogeTrabajoSegundos = 300, Robot("colgado"));
        m.Viriato.Trabajo["k-colgado"] = Trabajo(pendientes: 2);
        await m.Lanzador.CicloAsync(default);

        // Still waiting, still nobody working: the copy is hung.
        await m.Lanzador.CicloAsync(default);
        m.Reloj.Advance(TimeSpan.FromSeconds(301));
        await m.Lanzador.CicloAsync(default);

        Assert.True(m.Arrancador.Arrancadas[0].Parada);
        Assert.Equal(2, m.Arrancador.Arrancadas.Count);
        Assert.Equal(1, m.Lanzador.EnMarcha["colgado"]);
    }

    [Fact]
    public async Task ACopyBusyOnALongStep_IsLeftAlone()
    {
        var m = Montar(o => o.ReiniciarSiNoCogeTrabajoSegundos = 300, Robot("ocupado"));
        m.Viriato.Trabajo["k-ocupado"] = Trabajo(pendientes: 2);
        await m.Lanzador.CicloAsync(default);

        m.Viriato.Trabajo["k-ocupado"] = Trabajo(pendientes: 1, enEjecucion: 1);
        for (var i = 0; i < 5; i++)
        {
            m.Reloj.Advance(TimeSpan.FromMinutes(10));
            await m.Lanzador.CicloAsync(default);
        }

        Assert.False(Assert.Single(m.Arrancador.Arrancadas).Parada);
    }

    [Fact]
    public async Task WhenViriatoDoesNotAnswer_WhatRunsKeepsRunning_AndNothingNewStarts()
    {
        var m = Montar(null, Robot("extractor"), Robot("creador"));
        m.Viriato.Trabajo["k-extractor"] = Trabajo(pendientes: 1);
        await m.Lanzador.CicloAsync(default);
        m.Viriato.Reportado.Clear();

        m.Viriato.Fallos["k-extractor"] = new HttpRequestException("sin red");
        m.Viriato.Fallos["k-creador"] = new ClaveRechazadaException();
        await m.Lanzador.CicloAsync(default);

        Assert.False(Assert.Single(m.Arrancador.Arrancadas).Parada);
        Assert.Empty(m.Viriato.Reportado);
    }

    [Fact]
    public async Task AnInvalidCatalogue_StartsNothing()
    {
        var m = Montar(o => o.ViriatoUrl = "", Robot("extractor"));
        m.Viriato.Trabajo["k-extractor"] = Trabajo(pendientes: 1);

        await m.Lanzador.CicloAsync(default);

        Assert.Empty(m.Arrancador.Arrancadas);
        Assert.Empty(m.Viriato.Reportado);
    }

    [Fact]
    public async Task OnTheWayOut_EveryRunningRobotIsToldToStop()
    {
        var m = Montar(null, Robot("a"), Robot("b"));
        m.Viriato.Trabajo["k-a"] = Trabajo(pendientes: 1);
        m.Viriato.Trabajo["k-b"] = Trabajo(pendientes: 1);
        await m.Lanzador.CicloAsync(default);

        await m.Lanzador.PararTodoAsync();

        Assert.All(m.Arrancador.Arrancadas, c => Assert.True(c.Parada));
        Assert.All(m.Lanzador.EnMarcha.Values, copias => Assert.Equal(0, copias));
    }
}

/// <summary>The real thing, with a tiny shell script standing in for a robot.</summary>
public sealed class ArrancadorDeProcesosTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), $"lanzador-test-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, recursive: true);
    }

    private ArranqueDeRobot Arranque(string script, LanzadorOptions? opciones = null)
    {
        var robot = new RobotOptions { Nombre = "prueba", ClaveEnv = "CLAVE_PRUEBA", Comando = "/bin/sh", Argumentos = ["-c", script], Entorno = { ["Propio"] = "valor" } };
        var o = opciones ?? new LanzadorOptions { ViriatoUrl = "http://viriato.test", CarpetaTemporal = _carpeta };
        o.Robots = [robot, new RobotOptions { Nombre = "otro", ClaveEnv = "CLAVE_DE_OTRO_ROBOT", Comando = "x" }];
        return new ArranqueDeRobot(robot, "clave-de-prueba", null, o);
    }

    private static async Task EsperarAsync(Func<bool> condicion)
    {
        var limite = DateTime.UtcNow.AddSeconds(15);
        while (!condicion() && DateTime.UtcNow < limite) await Task.Delay(50);
        Assert.True(condicion(), "No ocurrió a tiempo.");
    }

    [Fact]
    public async Task ARobotGetsItsSettingsInItsEnvironment_AndItsExitCodeIsSeen()
    {
        if (OperatingSystem.IsWindows()) return;
        var salida = Path.Combine(Path.GetTempPath(), $"lanzador-salida-{Guid.NewGuid():N}.txt");
        try
        {
            var arrancador = new ArrancadorDeProcesos(_ => null);
            var copia = arrancador.Arrancar(Arranque($"echo \"$Viriato__ApiKey|$Viriato__BaseUrl|$Worker__SalirSinTrabajo|$Propio|$TMPDIR\" > '{salida}'; exit 3"));

            await EsperarAsync(() => !copia.Vivo);
            Assert.Equal(3, copia.CodigoDeSalida);

            var partes = (await File.ReadAllTextAsync(salida)).Trim().Split('|');
            Assert.Equal(["clave-de-prueba", "http://viriato.test", "00:00:30", "valor"], partes[..4]);
            Assert.StartsWith(Path.Combine(_carpeta, "prueba-"), partes[4]);

            // Its temporary folder goes with it.
            Assert.True(Directory.Exists(partes[4]));
            copia.Limpiar();
            Assert.False(Directory.Exists(partes[4]));
        }
        finally
        {
            File.Delete(salida);
        }
    }

    [Fact]
    public async Task ARobotNeverSeesAnotherRobotsKey()
    {
        if (OperatingSystem.IsWindows()) return;
        var salida = Path.Combine(Path.GetTempPath(), $"lanzador-salida-{Guid.NewGuid():N}.txt");
        Environment.SetEnvironmentVariable("CLAVE_DE_OTRO_ROBOT", "secreto-ajeno");
        try
        {
            var copia = new ArrancadorDeProcesos(Environment.GetEnvironmentVariable).Arrancar(Arranque($"echo \"[$CLAVE_DE_OTRO_ROBOT]\" > '{salida}'"));

            await EsperarAsync(() => !copia.Vivo);
            copia.Limpiar();

            Assert.Equal("[]", (await File.ReadAllTextAsync(salida)).Trim());
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLAVE_DE_OTRO_ROBOT", null);
            File.Delete(salida);
        }
    }

    [Fact]
    public async Task ARunningRobot_IsStoppedWhenTold()
    {
        if (OperatingSystem.IsWindows()) return;
        var copia = new ArrancadorDeProcesos(_ => null).Arrancar(Arranque("sleep 300"));
        Assert.True(copia.Vivo);

        await copia.PararAsync(TimeSpan.FromSeconds(5));

        Assert.False(copia.Vivo);
    }

    [Fact]
    public void ACommandThatDoesNotExist_FailsWithAMessageThatNamesIt_AndLeavesNoFolderBehind()
    {
        var arranque = Arranque("true");
        arranque.Robot.Comando = "/no/existe/robot";

        var ex = Assert.Throws<InvalidOperationException>(() => new ArrancadorDeProcesos(_ => null).Arrancar(arranque));

        Assert.Contains("/no/existe/robot", ex.Message);
        Assert.False(Directory.Exists(_carpeta) && Directory.EnumerateDirectories(_carpeta).Any());
    }
}
