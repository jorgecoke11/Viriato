using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Viariato.ApiContracts;

namespace Viriato.Rpa.Client;

/// <summary>What the robot's own machine says about the live view; whether it is on, and how smooth, is Viriato's decision.
/// Bound from the <c>VistaEnDirecto</c> configuration section.</summary>
public sealed class VistaEnDirectoOptions
{
    /// <summary>The robot's veto: false and the view is never served, whatever Viriato says (a PC that must not show its screen).</summary>
    public bool Permitida { get; set; } = true;

    /// <summary>The X display to show. Empty = the <c>DISPLAY</c> environment variable; with neither (Windows) there is nothing to show.</summary>
    public string? Pantalla { get; set; }

    /// <summary>Where noVNC is served (<c>http://…:PuertoWeb/vnc.html</c>).</summary>
    public int PuertoWeb { get; set; } = 6080;

    public int PuertoVnc { get; set; } = 5900;

    public string CarpetaNoVnc { get; set; } = "/usr/share/novnc";

    /// <summary>A VNC password every viewer must type. Not needed when the view is served behind Viriato's own login (<c>/vista/</c>).</summary>
    public string? Contrasena { get; set; }

    /// <summary>Let viewers move the mouse and type. Off: the view is for watching.</summary>
    public bool PermitirControl { get; set; }
}

/// <summary>Starts a program in the background; disposing what it returns stops it. Apart so the view can be tested without x11vnc.</summary>
public interface ILanzadorDeProcesos
{
    /// <summary>Null when the program could not be started (not installed…).</summary>
    IDisposable? Lanzar(string programa, IReadOnlyList<string> argumentos);
}

/// <summary>
/// Serves the robot's screen live with x11vnc + noVNC (websockify), the way Viriato decides for its Despliegue: off, or at a low,
/// medium or high refresh rate. A viewer costs the robot CPU (x11vnc and the X server keep reading the screen), so a slow robot can
/// be given a lighter view — or none — from Viriato's Despliegues screen, without touching the robot. Nothing is read while nobody
/// watches; what the setting changes is how hard it works when someone does. The picture quality is the viewer's part and Viriato
/// already puts it in the address it opens.
/// Register it with <see cref="ServiceCollectionExtensions.AddViriatoVistaEnDirecto"/>; the robot's container must no longer start
/// x11vnc itself.
/// </summary>
public sealed class VistaEnDirectoNoVnc : IAjusteDelDespliegue, IDisposable
{
    /// <summary>What a Viriato older than the setting gets: the view as robots served it before (x11vnc's own rate).</summary>
    public static readonly VistaEnDirectoDto SinDecisionDeViriato = new("Alta", MilisegundosEntreImagenes: 20, CalidadImagen: 6, Compresion: 2);

    private readonly VistaEnDirectoOptions _opciones;
    private readonly ILanzadorDeProcesos _lanzador;
    private readonly ILogger _logger;
    private readonly object _candado = new();
    private VistaEnDirectoDto? _aplicada;
    private bool _primeraVez = true;
    private IDisposable? _vnc;
    private IDisposable? _web;

    public VistaEnDirectoNoVnc(IOptions<VistaEnDirectoOptions> opciones, ILanzadorDeProcesos? lanzador = null, ILogger<VistaEnDirectoNoVnc>? logger = null)
    {
        _opciones = opciones.Value;
        _lanzador = lanzador ?? new LanzadorDeProcesos();
        _logger = logger ?? NullLogger<VistaEnDirectoNoVnc>.Instance;
    }

    /// <summary>What is being served now; null when the view is off.</summary>
    public VistaEnDirectoDto? Aplicada => _aplicada;

    public Task AplicarAsync(DespliegueEstadoDto estado, CancellationToken ct)
    {
        var deseada = estado.Pantalla is null ? SinDecisionDeViriato : estado.Pantalla.Vista;
        var pantalla = string.IsNullOrWhiteSpace(_opciones.Pantalla) ? Environment.GetEnvironmentVariable("DISPLAY") : _opciones.Pantalla;

        lock (_candado)
        {
            if (deseada is not null && (!_opciones.Permitida || string.IsNullOrWhiteSpace(pantalla)))
            {
                if (_primeraVez)
                {
                    _logger.LogInformation(_opciones.Permitida
                        ? "Viriato pide vista en directo, pero no hay pantalla (DISPLAY): no se sirve."
                        : "Viriato pide vista en directo, pero este robot no la permite (VistaEnDirecto:Permitida=false).");
                }

                deseada = null;
            }

            if (!_primeraVez && deseada == _aplicada) return Task.CompletedTask;
            _primeraVez = false;

            _vnc?.Dispose();
            _vnc = null;
            if (deseada is null)
            {
                _web?.Dispose();
                _web = null;
                if (_aplicada is not null) _logger.LogInformation("Vista en directo apagada.");
                _aplicada = null;
                return Task.CompletedTask;
            }

            _vnc = _lanzador.Lanzar("x11vnc", ArgumentosX11Vnc(_opciones, pantalla!, deseada, PrepararContrasena()));
            _web ??= _lanzador.Lanzar("websockify", ArgumentosWebsockify(_opciones));
            if (_vnc is null || _web is null)
            {
                _logger.LogWarning("No se pudo arrancar la vista en directo (¿están x11vnc y websockify instalados?).");
            }
            else
            {
                _logger.LogInformation("Vista en directo {Calidad}: una imagen cada {Ms} ms, en el puerto {Puerto}.",
                    deseada.Calidad, deseada.MilisegundosEntreImagenes, _opciones.PuertoWeb);
            }

            // Kept even if it failed: trying again every few seconds would only fill the log.
            _aplicada = deseada;
        }

        return Task.CompletedTask;
    }

    /// <summary>The arguments of x11vnc, apart so the choices can be tested without running it.</summary>
    public static IReadOnlyList<string> ArgumentosX11Vnc(VistaEnDirectoOptions o, string pantalla, VistaEnDirectoDto vista, string? ficheroContrasena)
    {
        var ms = Math.Max(1, vista.MilisegundosEntreImagenes).ToString(CultureInfo.InvariantCulture);
        var argumentos = new List<string>
        {
            "-display", pantalla, "-forever", "-shared", "-localhost", "-quiet",
            "-rfbport", o.PuertoVnc.ToString(CultureInfo.InvariantCulture),
            // How often it looks at the screen and how long it gathers changes before sending them: the CPU the view costs.
            "-wait", ms, "-defer", ms,
        };
        if (!o.PermitirControl) argumentos.Add("-viewonly");
        if (ficheroContrasena is null) argumentos.Add("-nopw");
        else argumentos.AddRange(["-passwdfile", $"rm:{ficheroContrasena}"]);
        return argumentos;
    }

    public static IReadOnlyList<string> ArgumentosWebsockify(VistaEnDirectoOptions o) =>
        ["--web", o.CarpetaNoVnc, o.PuertoWeb.ToString(CultureInfo.InvariantCulture), $"localhost:{o.PuertoVnc.ToString(CultureInfo.InvariantCulture)}"];

    // x11vnc reads the password from a file and deletes it ("rm:"), so it never shows in the process list.
    private string? PrepararContrasena()
    {
        if (string.IsNullOrEmpty(_opciones.Contrasena)) return null;
        var ruta = Path.Combine(Path.GetTempPath(), $"vnc-{Guid.NewGuid():N}.pass");
        File.WriteAllText(ruta, _opciones.Contrasena);
        return ruta;
    }

    public void Dispose()
    {
        lock (_candado)
        {
            _vnc?.Dispose();
            _web?.Dispose();
            _vnc = _web = null;
        }
    }
}

/// <summary>Runs real programs. Their output is thrown away: nobody reads it, and a full pipe would block them.</summary>
public sealed class LanzadorDeProcesos : ILanzadorDeProcesos
{
    public IDisposable? Lanzar(string programa, IReadOnlyList<string> argumentos)
    {
        var inicio = new ProcessStartInfo(programa) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argumento in argumentos) inicio.ArgumentList.Add(argumento);

        try
        {
            var proceso = Process.Start(inicio);
            if (proceso is null) return null;
            proceso.ErrorDataReceived += (_, _) => { };
            proceso.OutputDataReceived += (_, _) => { };
            proceso.BeginErrorReadLine();
            proceso.BeginOutputReadLine();
            return new ProcesoEnMarcha(proceso);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    private sealed class ProcesoEnMarcha(Process proceso) : IDisposable
    {
        public void Dispose()
        {
            try
            {
                if (!proceso.HasExited)
                {
                    proceso.Kill(entireProcessTree: true);
                    proceso.WaitForExit(2000);
                }
            }
            catch (InvalidOperationException) { /* already gone */ }
            finally
            {
                proceso.Dispose();
            }
        }
    }
}
