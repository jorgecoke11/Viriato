using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Viariato.ApiContracts;

namespace Viriato.Rpa.Lanzador;

/// <summary>
/// Keeps each robot of the catalogue running only while it has work. Every few seconds it asks Viriato what each robot has
/// waiting, starts the copies that are missing (see <see cref="Planificador"/>), looks after the ones running — a copy that
/// uses too much memory, or that should have left and has not — and tells Viriato what it is doing, so the machine's screen
/// can say what is started and why something is not.
/// </summary>
public sealed class LanzadorDeRobots(
    ICatalogoDelLanzador catalogo,
    IViriatoDelLanzador viriato,
    IArrancadorDeRobots arrancador,
    TimeProvider reloj,
    ILogger<LanzadorDeRobots> logger) : BackgroundService
{
    /// <summary>With the catalogue file and each robot's key in an environment variable.</summary>
    public LanzadorDeRobots(
        IOptionsMonitor<LanzadorOptions> opciones,
        IViriatoDelLanzador viriato,
        IArrancadorDeRobots arrancador,
        Func<string, string?> variable,
        TimeProvider reloj,
        ILogger<LanzadorDeRobots> logger)
        : this(new CatalogoDeArchivo(opciones, variable), viriato, arrancador, reloj, logger)
    {
    }

    // The settings of the last round: how often to look, how long to give a robot to stop.
    private LanzadorOptions _opciones = new();

    /// <summary>What the launcher remembers about one robot between rounds.</summary>
    private sealed class Memoria
    {
        public List<IRobotEnMarcha> Copias { get; } = [];
        public int FallosSeguidos { get; set; }
        public DateTimeOffset? FalloHasta { get; set; }
        public string? UltimoFallo { get; set; }

        /// <summary>The wait is not for a failure: the robot started, got no work and left.</summary>
        public bool FalloEsEspera { get; set; }

        /// <summary>What was waiting when its copies were started, and whether any of them has been seen working since.</summary>
        public int PendientesAlArrancar { get; set; }

        public bool VioTrabajo { get; set; }

        /// <summary>Since when its copies are running with nothing to do, according to Viriato.</summary>
        public DateTimeOffset? SinNadaDesde { get; set; }

        /// <summary>Since when work is waiting and no copy takes it.</summary>
        public DateTimeOffset? SinCogerDesde { get; set; }

        public string? UltimoAviso { get; set; }
    }

    private readonly Dictionary<string, Memoria> _robots = new(StringComparer.OrdinalIgnoreCase);
    private string? _ultimoProblemaDeCatalogo;

    /// <summary>Copies running right now, by robot. For the logs and the tests.</summary>
    public IReadOnlyDictionary<string, int> EnMarcha => _robots.ToDictionary(r => r.Key, r => r.Value.Copias.Count, StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Lanzador de robots en marcha.");
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CicloAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "La ronda del lanzador ha fallado; se reintenta.");
                }

                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _opciones.IntervaloSegundos)), reloj, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Told to stop.
        }
        finally
        {
            await PararTodoAsync().ConfigureAwait(false);
        }
    }

    /// <summary>One round: look after what is running, ask Viriato, start what is missing, report.</summary>
    public async Task CicloAsync(CancellationToken ct)
    {
        var leido = await catalogo.LeerAsync(_robots.Values.All(m => m.Copias.Count == 0), ct).ConfigureAwait(false);
        var o = _opciones = leido.Opciones;

        var problemas = leido.Problemas;
        if (problemas.Count > 0)
        {
            var texto = string.Join(Environment.NewLine, problemas.Select(p => "  - " + p));
            if (texto != _ultimoProblemaDeCatalogo && !leido.ProblemasYaContados)
            {
                logger.LogError("El catálogo del lanzador no es válido; no se arranca nada hasta corregirlo:{Salto}{Problemas}", Environment.NewLine, texto);
                _ultimoProblemaDeCatalogo = texto;
            }

            return;
        }

        _ultimoProblemaDeCatalogo = null;
        var ahora = reloj.GetUtcNow();
        var situaciones = new List<SituacionDeRobot>(o.Robots.Count);
        var claves = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var robot in o.Robots)
        {
            if (!_robots.TryGetValue(robot.Nombre, out var memoria)) _robots[robot.Nombre] = memoria = new Memoria();

            var clave = leido.Claves[robot.Nombre];
            claves[robot.Nombre] = clave;

            var trabajo = await PreguntarAsync(robot, memoria, clave, ct).ConfigureAwait(false);
            if (trabajo is { EnEjecucion: > 0 } && memoria.Copias.Count > 0) memoria.VioTrabajo = true;

            RecogerLasQueTerminaron(o, robot, memoria, trabajo, ahora);
            await VigilarMemoriaAsync(o, robot, memoria, ahora).ConfigureAwait(false);
            if (trabajo is not null) await VigilarAtascosAsync(o, robot, memoria, trabajo, ahora).ConfigureAwait(false);

            situaciones.Add(new SituacionDeRobot(
                robot, trabajo, memoria.Copias.Count, memoria.FalloHasta, memoria.UltimoFallo, memoria.FalloEsEspera));
        }

        // A robot taken out of the catalogue: what it has running finishes by itself, and then it is forgotten.
        foreach (var nombre in _robots.Keys.Where(n => o.Robots.All(r => !r.Nombre.Equals(n, StringComparison.OrdinalIgnoreCase))).ToList())
        {
            var memoria = _robots[nombre];
            foreach (var copia in memoria.Copias.Where(c => !c.Vivo).ToList())
            {
                copia.Limpiar();
                memoria.Copias.Remove(copia);
            }

            if (memoria.Copias.Count == 0) _robots.Remove(nombre);
        }

        var decisiones = Planificador.Decidir(o, situaciones, ahora);

        for (var i = 0; i < decisiones.Count; i++)
        {
            var decision = decisiones[i];
            var robot = situaciones[i].Robot;
            var memoria = _robots[robot.Nombre];
            var estado = decision.Estado;
            var detalle = decision.Detalle;

            for (var n = 0; n < decision.Arrancar; n++)
            {
                try
                {
                    var hueco = robot.Pantalla ? HuecoDePantallaLibre() : (int?)null;
                    if (memoria.Copias.Count == 0)
                    {
                        memoria.PendientesAlArrancar = situaciones[i].Trabajo?.Pendientes ?? 0;
                        memoria.VioTrabajo = false;
                    }

                    memoria.Copias.Add(arrancador.Arrancar(new ArranqueDeRobot(robot, claves[robot.Nombre], hueco, o)));
                    logger.LogInformation("Arrancada una copia de {Robot} ({Copias} en marcha).", robot.Nombre, memoria.Copias.Count);
                }
                catch (Exception ex)
                {
                    ApuntarFallo(o, robot, memoria, $"No se pudo arrancar: {ex.Message}", ahora);
                    if (memoria.Copias.Count == 0) estado = EstadosDelLanzador.Fallo;
                    detalle = memoria.UltimoFallo;
                    break;
                }
            }

            if (estado is null) continue;

            try
            {
                await viriato.ReportarAsync(
                    claves[robot.Nombre], new EstadoDelLanzadorRequest(estado, memoria.Copias.Count, Math.Max(0, robot.MaxCopias), detalle), ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogDebug(ex, "No se pudo contar a Viriato el estado de {Robot}.", robot.Nombre);
            }
        }
    }

    // A copy that ended by itself: with 0 it emptied its queue and left, which is the plan; anything else is a failure.
    private void RecogerLasQueTerminaron(LanzadorOptions o, RobotOptions robot, Memoria memoria, TrabajoDelDespliegueDto? trabajo, DateTimeOffset ahora)
    {
        foreach (var copia in memoria.Copias.Where(c => !c.Vivo).ToList())
        {
            var codigo = copia.CodigoDeSalida ?? 0;
            copia.Limpiar();
            memoria.Copias.Remove(copia);

            // It left with work still waiting and without having done any (nothing seen in execution, the queue no shorter):
            // Viriato did not hand it anything — not its turn on a machine with a ceiling, most likely. Starting it again at
            // once would only repeat that, so it waits, a little longer each time.
            var seFueSinTrabajar = codigo == 0 && memoria.Copias.Count == 0
                && trabajo is { Pendientes: > 0 } && !memoria.VioTrabajo && trabajo.Pendientes >= memoria.PendientesAlArrancar;

            if (seFueSinTrabajar)
            {
                ApuntarFallo(o, robot, memoria, "Arrancó, Viriato no le dio trabajo (¿no era su turno?) y se apagó.", ahora, esEspera: true);
            }
            else if (codigo == 0)
            {
                logger.LogInformation("Una copia de {Robot} ha terminado: sin trabajo ({Copias} en marcha).", robot.Nombre, memoria.Copias.Count);
                memoria.FallosSeguidos = 0;
                memoria.FalloHasta = null;
                memoria.UltimoFallo = null;
                memoria.FalloEsEspera = false;
            }
            else
            {
                ApuntarFallo(o, robot, memoria, $"El robot terminó con un error (código {codigo}); mira su registro.", ahora);
            }
        }
    }

    private async Task VigilarMemoriaAsync(LanzadorOptions o, RobotOptions robot, Memoria memoria, DateTimeOffset ahora)
    {
        if (robot.MemoriaMaximaMb is not { } tope) return;

        foreach (var copia in memoria.Copias.ToList())
        {
            if (copia.MemoriaMb() is not { } usa || usa <= tope) continue;

            await copia.PararAsync(TimeSpan.Zero).ConfigureAwait(false);
            memoria.Copias.Remove(copia);
            ApuntarFallo(o, robot, memoria, $"Una copia usaba {usa} MB, más de los {tope} MB que tiene de tope, y se ha parado.", ahora);
        }
    }

    private async Task<TrabajoDelDespliegueDto?> PreguntarAsync(RobotOptions robot, Memoria memoria, string clave, CancellationToken ct)
    {
        try
        {
            var trabajo = await viriato.TrabajoAsync(clave, ct).ConfigureAwait(false);
            memoria.UltimoAviso = null;
            return trabajo;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var aviso = ex is ClaveRechazadaException
                ? string.IsNullOrEmpty(robot.ClaveEnv)
                    ? $"Viriato rechaza el pase de {robot.Nombre}; se pide otro en la próxima ronda."
                    : $"Viriato rechaza la clave de {robot.Nombre} (variable {robot.ClaveEnv}): revisa que sea la de su despliegue y que no se haya regenerado."
                : $"No se pudo preguntar a Viriato por {robot.Nombre}: {ex.Message}";

            // Said once, not every few seconds while it lasts.
            if (aviso != memoria.UltimoAviso) logger.LogWarning("{Aviso}", aviso);
            memoria.UltimoAviso = aviso;
            return null;
        }
    }

    // Copies that are running when, by what Viriato says, they should not be — or that are there and do not work.
    private async Task VigilarAtascosAsync(LanzadorOptions o, RobotOptions robot, Memoria memoria, TrabajoDelDespliegueDto trabajo, DateTimeOffset ahora)
    {
        if (memoria.Copias.Count == 0)
        {
            memoria.SinNadaDesde = memoria.SinCogerDesde = null;
            return;
        }

        // Nothing held and nothing waiting: each copy leaves by itself after SalirSinTrabajo. One still here well past that
        // is hung, or was built with a client that does not know how to leave.
        if (trabajo is { Pendientes: 0, EnEjecucion: 0 })
        {
            memoria.SinNadaDesde ??= ahora;
            var limite = TimeSpan.FromSeconds(Math.Max(1, o.SalirSinTrabajoSegundos) + Math.Max(0, o.GraciaParaSalirSegundos));
            if (ahora - memoria.SinNadaDesde >= limite)
            {
                logger.LogWarning(
                    "{Robot} no tiene trabajo desde hace {Tiempo} y sigue en marcha: se para. (¿Usa Viriato.Rpa.Client 0.12 o posterior?)",
                    robot.Nombre, ahora - memoria.SinNadaDesde);
                await PararCopiasAsync(o, memoria).ConfigureAwait(false);
            }
        }
        else
        {
            memoria.SinNadaDesde = null;
        }

        // Work waiting, copies running, none of them working — and Viriato is not holding the work back.
        var noCogen = o.ReiniciarSiNoCogeTrabajoSegundos > 0 && memoria.Copias.Count > 0
            && trabajo is { Encendido: true, ServicioAlLimite: false, Pendientes: > 0, EnEjecucion: 0 };
        if (noCogen)
        {
            memoria.SinCogerDesde ??= ahora;
            if (ahora - memoria.SinCogerDesde >= TimeSpan.FromSeconds(o.ReiniciarSiNoCogeTrabajoSegundos))
            {
                logger.LogWarning(
                    "{Robot} tiene trabajo esperando desde hace {Tiempo} y ninguna copia lo coge: se reinicia.", robot.Nombre, ahora - memoria.SinCogerDesde);
                await PararCopiasAsync(o, memoria).ConfigureAwait(false);
            }
        }
        else
        {
            memoria.SinCogerDesde = null;
        }
    }

    private static async Task PararCopiasAsync(LanzadorOptions o, Memoria memoria)
    {
        var gracia = TimeSpan.FromSeconds(Math.Max(0, o.GraciaAlPararSegundos));
        await Task.WhenAll(memoria.Copias.Select(c => c.PararAsync(gracia))).ConfigureAwait(false);
        memoria.Copias.Clear();
        memoria.SinNadaDesde = memoria.SinCogerDesde = null;
    }

    private void ApuntarFallo(LanzadorOptions o, RobotOptions robot, Memoria memoria, string motivo, DateTimeOffset ahora, bool esEspera = false)
    {
        memoria.FallosSeguidos++;
        memoria.UltimoFallo = motivo;
        memoria.FalloEsEspera = esEspera;
        var espera = Planificador.EsperaTrasFallo(o, memoria.FallosSeguidos);
        memoria.FalloHasta = ahora + espera;
        if (esEspera) logger.LogWarning("{Robot}: {Motivo} No se vuelve a arrancar hasta dentro de {Espera}.", robot.Nombre, motivo, espera);
        else logger.LogError("{Robot}: {Motivo} No se vuelve a intentar hasta dentro de {Espera}.", robot.Nombre, motivo, espera);
    }

    // The lowest screen slot no running copy has: it decides the display and the ports of the live view.
    private int HuecoDePantallaLibre()
    {
        var usados = _robots.Values.SelectMany(m => m.Copias).Select(c => c.HuecoDePantalla).OfType<int>().ToHashSet();
        var hueco = 0;
        while (usados.Contains(hueco)) hueco++;
        return hueco;
    }

    /// <summary>On the way out: every robot gets the chance to finish (or report as failed) what it is doing.</summary>
    public async Task PararTodoAsync()
    {
        var copias = _robots.Values.SelectMany(m => m.Copias).ToList();
        if (copias.Count == 0) return;

        logger.LogInformation("Parando {Copias} robots en marcha…", copias.Count);
        var gracia = TimeSpan.FromSeconds(Math.Max(0, _opciones.GraciaAlPararSegundos));
        await Task.WhenAll(copias.Select(c => c.PararAsync(gracia))).ConfigureAwait(false);
        foreach (var memoria in _robots.Values) memoria.Copias.Clear();
    }
}
