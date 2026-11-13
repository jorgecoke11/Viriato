using System.Diagnostics;
using System.Globalization;

namespace Viriato.Rpa.Lanzador;

/// <summary>Everything needed to start one copy of a robot.</summary>
/// <param name="Clave">The key of its Despliegue: handed to the robot, never written anywhere.</param>
/// <param name="HuecoDePantalla">For a robot with a screen, which of the machine's screen slots it gets (0 = the first): it
/// sets its display and the ports of its live view, so two browsers never share a screen. Null: no screen.</param>
public sealed record ArranqueDeRobot(RobotOptions Robot, string Clave, int? HuecoDePantalla, LanzadorOptions Opciones);

/// <summary>A copy of a robot that was started.</summary>
public interface IRobotEnMarcha
{
    bool Vivo { get; }

    /// <summary>How it ended; null while it runs.</summary>
    int? CodigoDeSalida { get; }

    int? HuecoDePantalla { get; }

    /// <summary>Memory used by it and by everything it started (its browser…), or null if it cannot be told.</summary>
    long? MemoriaMb();

    /// <summary>Asks it to stop, waits that long for it to finish what it is doing, then kills it — and cleans up after it.</summary>
    Task PararAsync(TimeSpan gracia);

    /// <summary>For a copy that ended by itself: stops what it left behind (its screen, a browser that outlived it) and
    /// deletes its temporary folder.</summary>
    void Limpiar();
}

/// <summary>Starts copies of robots. Apart from the launcher's logic so that logic can be tested without processes.</summary>
public interface IArrancadorDeRobots
{
    /// <summary>Throws, with a message a person can act on, if the robot cannot be started.</summary>
    IRobotEnMarcha Arrancar(ArranqueDeRobot arranque);
}

/// <summary>What a robot's process is given: where Viriato is, its key, when to leave, and a place of its own.</summary>
public static class EntornoDelRobot
{
    public static Dictionary<string, string> Construir(ArranqueDeRobot arranque, string carpetaTemporal)
    {
        var o = arranque.Opciones;
        var entorno = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Viriato__BaseUrl"] = o.ViriatoUrl,
            ["Viriato__ApiKey"] = arranque.Clave,
            // The robot empties its queue and gets out of the way (Viriato.Rpa.Client 0.12+).
            ["Worker__SalirSinTrabajo"] = TimeSpan.FromSeconds(Math.Max(1, o.SalirSinTrabajoSegundos)).ToString("c", CultureInfo.InvariantCulture),
            ["TMPDIR"] = carpetaTemporal,
        };

        var sustituciones = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (arranque.HuecoDePantalla is { } hueco)
        {
            var pantalla = Pantalla(o, hueco);
            var puertoWeb = (o.PuertoWebBase + hueco).ToString(CultureInfo.InvariantCulture);
            var puertoVnc = (o.PuertoVncBase + hueco).ToString(CultureInfo.InvariantCulture);
            entorno["DISPLAY"] = pantalla;
            entorno["VistaEnDirecto__PuertoWeb"] = puertoWeb;
            entorno["VistaEnDirecto__PuertoVnc"] = puertoVnc;
            sustituciones["{pantalla}"] = pantalla;
            sustituciones["{puertoWeb}"] = puertoWeb;
            sustituciones["{puertoVnc}"] = puertoVnc;
        }

        foreach (var (clave, valorCrudo) in arranque.Robot.Entorno)
        {
            var valor = valorCrudo ?? string.Empty;
            foreach (var (marca, por) in sustituciones) valor = valor.Replace(marca, por, StringComparison.OrdinalIgnoreCase);
            entorno[clave] = valor;
        }

        return entorno;
    }

    public static string Pantalla(LanzadorOptions opciones, int hueco) => $":{opciones.PantallaBase + hueco}";
}

/// <summary>
/// Starts each copy as a process of its own, in its own session (so it and whatever it starts — a browser, the live view — can
/// be stopped and measured together), with a temporary folder of its own and, if it needs a screen, a virtual one of its own.
/// </summary>
public sealed class ArrancadorDeProcesos : IArrancadorDeRobots
{
    private const string Setsid = "/usr/bin/setsid";

    public IRobotEnMarcha Arrancar(ArranqueDeRobot arranque)
    {
        var robot = arranque.Robot;
        var id = Guid.NewGuid().ToString("N")[..8];
        // The name is a person's (a service's name, from Viriato): only what is safe in a folder name.
        var seguro = new string(robot.Nombre.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '-').ToArray());
        var carpeta = Path.Combine(arranque.Opciones.CarpetaTemporal, $"{seguro}-{id}");
        Directory.CreateDirectory(carpeta);

        var entorno = EntornoDelRobot.Construir(arranque, carpeta);
        var auxiliares = new List<Process>();
        try
        {
            if (arranque.HuecoDePantalla is { } hueco) auxiliares.AddRange(ArrancarPantalla(arranque.Opciones, hueco));

            // In a session of its own where the system allows it: its pid is then also the id of everything it starts.
            var conSesion = File.Exists(Setsid);
            var inicio = new ProcessStartInfo
            {
                FileName = conSesion ? Setsid : robot.Comando,
                WorkingDirectory = string.IsNullOrWhiteSpace(robot.Directorio) ? Environment.CurrentDirectory : robot.Directorio,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
            };
            if (conSesion) inicio.ArgumentList.Add(robot.Comando);
            foreach (var argumento in robot.Argumentos) inicio.ArgumentList.Add(argumento);

            // A robot never sees the launcher's own settings (where the secret shared with Viriato is, above all).
            foreach (var propia in inicio.Environment.Keys.Where(k => k.StartsWith("LANZADOR_", StringComparison.Ordinal)).ToList())
            {
                inicio.Environment.Remove(propia);
            }

            foreach (var (clave, valor) in entorno) inicio.Environment[clave] = valor;

            var proceso = new Process { StartInfo = inicio, EnableRaisingEvents = true };
            var etiqueta = $"[{robot.Nombre}#{id}]";
            proceso.OutputDataReceived += (_, e) => { if (e.Data is not null) Console.Out.WriteLine($"{etiqueta} {e.Data}"); };
            proceso.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.Error.WriteLine($"{etiqueta} {e.Data}"); };

            try
            {
                proceso.Start();
            }
            catch (Exception ex)
            {
                proceso.Dispose();
                throw new InvalidOperationException($"No se pudo ejecutar \"{robot.Comando}\": {ex.Message}", ex);
            }

            proceso.StandardInput.Close();
            proceso.BeginOutputReadLine();
            proceso.BeginErrorReadLine();
            return new RobotComoProceso(proceso, conSesion, arranque.HuecoDePantalla, auxiliares, carpeta);
        }
        catch
        {
            foreach (var auxiliar in auxiliares) RobotComoProceso.Matar(auxiliar);
            RobotComoProceso.BorrarCarpeta(carpeta);
            throw;
        }
    }

    // A virtual screen for one robot: Xvfb and a minimal window manager (without one a browser cannot be maximised).
    private static List<Process> ArrancarPantalla(LanzadorOptions opciones, int hueco)
    {
        var numero = opciones.PantallaBase + hueco;
        var pantalla = EntornoDelRobot.Pantalla(opciones, hueco);
        var enchufe = $"/tmp/.X11-unix/X{numero}";

        // What a screen that ended badly leaves behind keeps the next one from starting.
        foreach (var resto in new[] { $"/tmp/.X{numero}-lock", enchufe })
        {
            try { File.Delete(resto); } catch (Exception) { /* not ours to delete, or not there */ }
        }

        Process xvfb;
        try
        {
            xvfb = Process.Start(new ProcessStartInfo("Xvfb", [pantalla, "-screen", "0", "1920x1080x24", "-nolisten", "tcp"])
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            })!;
            xvfb.BeginOutputReadLine();
            xvfb.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Este robot necesita pantalla y no se pudo arrancar Xvfb ({ex.Message}). Usa la imagen del lanzador con navegador.", ex);
        }

        var espera = Stopwatch.StartNew();
        while (!File.Exists(enchufe) && !xvfb.HasExited && espera.Elapsed < TimeSpan.FromSeconds(10)) Thread.Sleep(100);
        if (xvfb.HasExited || !File.Exists(enchufe))
        {
            RobotComoProceso.Matar(xvfb);
            throw new InvalidOperationException($"La pantalla virtual {pantalla} no ha arrancado.");
        }

        var auxiliares = new List<Process> { xvfb };
        try
        {
            var gestor = new ProcessStartInfo("openbox") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            gestor.Environment["DISPLAY"] = pantalla;
            var openbox = Process.Start(gestor)!;
            openbox.BeginOutputReadLine();
            openbox.BeginErrorReadLine();
            auxiliares.Add(openbox);
        }
        catch (Exception)
        {
            // The robot can still work; maximising its browser window may fail.
            Console.Error.WriteLine($"AVISO: no se pudo arrancar openbox en {pantalla}; el navegador no podrá maximizarse.");
        }

        return auxiliares;
    }
}

internal sealed class RobotComoProceso(Process proceso, bool conSesion, int? huecoDePantalla, List<Process> auxiliares, string carpeta) : IRobotEnMarcha
{
    private readonly int _pid = proceso.Id;
    private bool _limpio;
    private int? _codigoAlLimpiar;

    // Once cleaned up the process object is gone; what it ended with is remembered.
    public bool Vivo => !_limpio && !proceso.HasExited;

    public int? CodigoDeSalida => _limpio ? _codigoAlLimpiar : proceso.HasExited ? proceso.ExitCode : null;

    public int? HuecoDePantalla => huecoDePantalla;

    public long? MemoriaMb()
    {
        try
        {
            if (_limpio) return null;
            if (conSesion && Directory.Exists("/proc")) return MemoriaDeLaSesion(_pid) / (1024 * 1024);
            if (_limpio || proceso.HasExited) return null;
            proceso.Refresh();
            return proceso.WorkingSet64 / (1024 * 1024);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task PararAsync(TimeSpan gracia)
    {
        if (!proceso.HasExited && gracia > TimeSpan.Zero && !OperatingSystem.IsWindows())
        {
            // The polite way first: a robot told to stop reports the step it was on as failed, so it can be reprocessed.
            Senal("-TERM", conSesion ? $"-{_pid}" : _pid.ToString(CultureInfo.InvariantCulture));
            using var limite = new CancellationTokenSource(gracia);
            try
            {
                await proceso.WaitForExitAsync(limite.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // It did not make it in time.
            }
        }

        Matar(proceso);
        Limpiar();
    }

    public void Limpiar()
    {
        if (_limpio) return;
        try
        {
            _codigoAlLimpiar = proceso.HasExited ? proceso.ExitCode : null;
        }
        catch (Exception)
        {
            _codigoAlLimpiar = null;
        }

        _limpio = true;

        // Whatever the robot started and left running (a browser, the live view) is in its session.
        if (conSesion) Senal("-KILL", $"-{_pid}");
        foreach (var auxiliar in auxiliares) Matar(auxiliar);
        BorrarCarpeta(carpeta);
        proceso.Dispose();
    }

    public static void Matar(Process p)
    {
        try
        {
            if (!p.HasExited) p.Kill(entireProcessTree: true);
            p.WaitForExit(5000);
        }
        catch (Exception)
        {
            // Already gone.
        }
    }

    public static void BorrarCarpeta(string ruta)
    {
        try
        {
            if (Directory.Exists(ruta)) Directory.Delete(ruta, recursive: true);
        }
        catch (Exception)
        {
            // Best effort: something in it is still in use.
        }
    }

    private static void Senal(string senal, string destino)
    {
        try
        {
            // Through the shell: its own "kill" is always there, the program of that name is not (slim images lack it).
            using var kill = Process.Start(new ProcessStartInfo("/bin/sh", ["-c", $"kill {senal} -- {destino}"])
            {
                UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true,
            });
            kill?.WaitForExit(2000);
        }
        catch (Exception)
        {
            // No "kill" here: Matar does the rest.
        }
    }

    // Resident memory of every process whose session is the robot's (Linux: /proc/<pid>/stat and statm).
    private static long MemoriaDeLaSesion(int sesion)
    {
        long paginas = 0;
        foreach (var carpetaDeProceso in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(carpetaDeProceso), out _)) continue;
            try
            {
                var stat = File.ReadAllText(Path.Combine(carpetaDeProceso, "stat"));
                // "pid (name) state ppid pgrp session …" — the name may itself contain spaces and brackets.
                var campos = stat[(stat.LastIndexOf(')') + 2)..].Split(' ');
                if (campos.Length < 4 || !int.TryParse(campos[3], out var suSesion) || suSesion != sesion) continue;

                var statm = File.ReadAllText(Path.Combine(carpetaDeProceso, "statm")).Split(' ');
                if (statm.Length > 1 && long.TryParse(statm[1], out var residentes)) paginas += residentes;
            }
            catch (Exception)
            {
                // The process ended while it was being read.
            }
        }

        return paginas * Environment.SystemPageSize;
    }
}
