using Viariato.ApiContracts;

namespace Viriato.Rpa.Lanzador;

/// <summary>What is known about one robot when a round is decided.</summary>
/// <param name="Trabajo">What Viriato said about its queue, or null if it could not be asked this round (nothing is decided
/// for it then: what runs keeps running, nothing new starts).</param>
/// <param name="Vivas">Copies of it running on the launcher right now.</param>
/// <param name="FalloHasta">It failed recently: no new copy until then.</param>
/// <param name="UltimoFallo">What went wrong that time, for whoever looks.</param>
/// <param name="FalloEsEspera">That "failure" was not one: the robot started fine, was handed nothing and left (it was not its
/// turn). It is still not started again right away, but it is reported as waiting, not as broken.</param>
public sealed record SituacionDeRobot(
    RobotOptions Robot, TrabajoDelDespliegueDto? Trabajo, int Vivas, DateTimeOffset? FalloHasta = null, string? UltimoFallo = null,
    bool FalloEsEspera = false);

/// <summary>What to do about one robot this round, and what to tell Viriato about it.</summary>
/// <param name="Arrancar">Copies to start now.</param>
/// <param name="Estado">One of <see cref="EstadosDelLanzador"/>, or null when there is nothing to report (Viriato could not be asked).</param>
public sealed record DecisionDeRobot(string Nombre, int Arrancar, string? Estado, string? Detalle);

/// <summary>
/// Decides, each round, how many copies of each robot to start. Pure: it is told what Viriato said and what is running, and
/// answers — so every rule can be checked without a process or a network.
///
/// It only decides <b>when a robot is running</b>. Which step each copy gets is Viriato's business, as always: a started
/// copy asks for work like any robot. The rules:
/// <list type="number">
/// <item>A robot wants one copy per step it has (running or waiting), up to its replicas (set on its Despliegue in Viriato). Copies already running
/// and idle count first: nothing new is started while an idle copy could take the work.</item>
/// <item>Nothing is started for a robot that is switched off in Viriato, whose service is at its global cap, or that
/// failed a moment ago (it waits, a little longer after each failure in a row).</item>
/// <item>The machine has a ceiling of copies at once, and a lower one for copies with a screen. When there is not room
/// for everybody, the catalogue's order is the priority.</item>
/// <item>A running copy is never stopped here: it leaves by itself once its queue is empty.</item>
/// <item>A robot that was started, was handed nothing by Viriato and left (it was not its turn) is not started again at
/// once either: it waits like one that failed, but is reported as waiting.</item>
/// </list>
/// </summary>
public static class Planificador
{
    public static IReadOnlyList<DecisionDeRobot> Decidir(LanzadorOptions opciones, IReadOnlyList<SituacionDeRobot> robots, DateTimeOffset ahora)
    {
        var enMarcha = robots.Sum(r => r.Vivas);
        var conPantalla = robots.Where(r => r.Robot.Pantalla).Sum(r => r.Vivas);
        var decisiones = new List<DecisionDeRobot>(robots.Count);

        foreach (var situacion in robots)
        {
            var robot = situacion.Robot;
            if (situacion.Trabajo is not { } trabajo)
            {
                decisiones.Add(new DecisionDeRobot(robot.Nombre, 0, null, null));
                continue;
            }

            var replicas = Math.Max(0, robot.Replicas);
            var ocupadas = Math.Min(situacion.Vivas, trabajo.EnEjecucion);
            var deseadas = Math.Min(replicas, ocupadas + trabajo.Pendientes);
            var faltan = Math.Max(0, deseadas - situacion.Vivas);

            string? porQueNo = null;
            var arrancar = 0;
            var fallo = false;

            if (!trabajo.Encendido)
            {
                faltan = 0;
            }
            else if (faltan > 0 && situacion.FalloHasta is { } hasta && hasta > ahora)
            {
                fallo = !situacion.FalloEsEspera;
                porQueNo = $"{situacion.UltimoFallo ?? "El robot falló al arrancar."} Se reintenta a las {hasta.ToLocalTime():HH:mm:ss}.";
            }
            else if (faltan > 0 && trabajo.ServicioAlLimite)
            {
                porQueNo = "El servicio está en su límite global de ejecuciones.";
            }
            else
            {
                while (arrancar < faltan)
                {
                    if (enMarcha >= opciones.MaxRobotsALaVez)
                    {
                        porQueNo = $"Esperando hueco: el lanzador ya tiene {enMarcha} de {opciones.MaxRobotsALaVez} robots en marcha.";
                        break;
                    }

                    if (robot.Pantalla && conPantalla >= opciones.MaxConPantalla)
                    {
                        porQueNo = opciones.MaxConPantalla == 0
                            ? "El lanzador no admite robots con navegador: en Viriato, «Lanzador» tiene 0 con navegador."
                            : $"Esperando la pantalla: ya hay {conPantalla} de {opciones.MaxConPantalla} robots con navegador en marcha.";
                        break;
                    }

                    arrancar++;
                    enMarcha++;
                    if (robot.Pantalla) conPantalla++;
                }
            }

            if (trabajo.Encendido && replicas == 0 && trabajo.Pendientes > 0)
            {
                porQueNo = "Está aparcado: tiene 0 réplicas en su despliegue.";
            }

            var copias = situacion.Vivas + arrancar;
            var estado =
                !trabajo.Encendido ? EstadosDelLanzador.Apagado
                : fallo && copias == 0 ? EstadosDelLanzador.Fallo
                : copias > 0 ? EstadosDelLanzador.EnMarcha
                : porQueNo is not null ? EstadosDelLanzador.EnEspera
                : EstadosDelLanzador.SinTrabajo;

            decisiones.Add(new DecisionDeRobot(robot.Nombre, arrancar, estado, porQueNo));
        }

        return decisiones;
    }

    /// <summary>How long to wait before trying a robot again after its n-th failure in a row: the base, doubled each time,
    /// up to fifteen minutes.</summary>
    public static TimeSpan EsperaTrasFallo(LanzadorOptions opciones, int fallosSeguidos)
    {
        var baseSegundos = Math.Max(1, opciones.EsperaTrasFalloSegundos);
        var segundos = baseSegundos * Math.Pow(2, Math.Clamp(fallosSeguidos - 1, 0, 10));
        return TimeSpan.FromSeconds(Math.Min(segundos, 15 * 60));
    }
}
