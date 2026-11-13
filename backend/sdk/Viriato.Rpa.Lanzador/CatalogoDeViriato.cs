using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Viariato.ApiContracts;

namespace Viriato.Rpa.Lanzador;

/// <summary>Where the launcher that lives next to Viriato keeps things.</summary>
/// <param name="ArchivoSecreto">The secret shared with Viriato: a file Viriato creates on a volume both mount.</param>
/// <param name="CarpetaDeRobots">Where downloaded robot versions are unpacked, one folder per version.</param>
public sealed record LanzadorJuntoAViriato(string ArchivoSecreto, string CarpetaDeRobots);

/// <summary>
/// The catalogue as Viriato has it: every Despliegue with a robot version uploaded, how to run it, and a pass for it. Asked every
/// round, so a new version, a robot switched off or a new robot needs nothing done on the server. Each version is downloaded once,
/// checked against its hash, and unpacked in its own folder; a new one is used by the next copy started, never swapped under a
/// running one.
/// </summary>
public sealed class CatalogoDeViriato(
    HttpClient http, LanzadorOptions baseDeOpciones, LanzadorJuntoAViriato donde, ILogger<CatalogoDeViriato> logger) : ICatalogoDelLanzador
{
    private CatalogoLeido? _ultimo;
    private readonly Dictionary<Guid, string> _avisosPorVersion = [];
    private string? _ultimoAviso;

    public async Task<CatalogoLeido> LeerAsync(bool nadaEnMarcha, CancellationToken ct)
    {
        CatalogoDelLanzadorDto catalogo;
        try
        {
            catalogo = await PedirAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Avisar(ex is ProblemaDelCatalogo ? ex.Message : $"No se pudo leer el catálogo de Viriato: {ex.Message}");
            // What runs keeps being looked after with what was known; nothing new is asked of Viriato's robots meanwhile.
            return _ultimo ?? new CatalogoLeido(Opciones([]), new Dictionary<string, string>(), [_ultimoAviso!]);
        }

        Avisar(null);
        var robots = new List<RobotOptions>();
        var claves = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var robot in catalogo.Robots)
        {
            var carpeta = await PrepararAsync(robot, ct).ConfigureAwait(false);
            if (carpeta is null) continue;

            robots.Add(new RobotOptions
            {
                Nombre = robot.Nombre,
                Comando = robot.Comando,
                Argumentos = [.. robot.Argumentos],
                Directorio = carpeta,
                Replicas = robot.Replicas,
                Pantalla = robot.Pantalla,
                MemoriaMaximaMb = robot.MemoriaMaximaMb,
                Entorno = new Dictionary<string, string>(robot.Entorno),
            });
            claves[robot.Nombre] = robot.Clave;
        }

        if (nadaEnMarcha) Ordenar(catalogo.Robots.Select(r => r.VersionId.ToString("N")).ToHashSet());

        var opciones = Opciones(robots);
        opciones.MaxRobotsALaVez = catalogo.MaxRobotsALaVez;
        opciones.MaxConPantalla = catalogo.MaxConPantalla;
        _ultimo = new CatalogoLeido(opciones, claves, []);
        return _ultimo;
    }

    private sealed class ProblemaDelCatalogo(string mensaje) : Exception(mensaje);

    private async Task<CatalogoDelLanzadorDto> PedirAsync(CancellationToken ct)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Get, "api/v1/rpa/lanzador/catalogo");
        peticion.Headers.Add(RpaHeaders.SecretoDelLanzador, LeerSecreto());
        using var respuesta = await http.SendAsync(peticion, ct).ConfigureAwait(false);
        if (respuesta.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new ProblemaDelCatalogo("Viriato no reconoce el secreto del lanzador: ¿se ha borrado o cambiado? Reinicia el lanzador.");
        }

        if (respuesta.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ProblemaDelCatalogo("Este Viriato no tiene lanzador configurado (falta Lanzador__ArchivoSecreto en su API).");
        }

        respuesta.EnsureSuccessStatusCode();
        return await respuesta.Content.ReadFromJsonAsync<CatalogoDelLanzadorDto>(ct).ConfigureAwait(false)
            ?? throw new HttpRequestException("Viriato contestó sin datos.");
    }

    private string LeerSecreto()
    {
        try
        {
            var secreto = File.ReadAllText(donde.ArchivoSecreto).Trim();
            if (secreto.Length > 0) return secreto;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not there yet: Viriato creates it when its API starts.
        }

        throw new ProblemaDelCatalogo($"Esperando el secreto que crea la API de Viriato al arrancar ({donde.ArchivoSecreto}).");
    }

    /// <summary>The folder the robot runs in, downloading and unpacking its version the first time. Null if it cannot be had.</summary>
    private async Task<string?> PrepararAsync(RobotDelCatalogoDto robot, CancellationToken ct)
    {
        var carpeta = Path.Combine(donde.CarpetaDeRobots, robot.VersionId.ToString("N"));
        var dondeCorre = Path.Combine(carpeta, robot.Raiz);
        if (Directory.Exists(carpeta)) return dondeCorre;

        var temporal = carpeta + ".descargando";
        try
        {
            if (Directory.Exists(temporal)) Directory.Delete(temporal, recursive: true);
            Directory.CreateDirectory(temporal);
            var zip = Path.Combine(temporal, "robot.zip");

            using (var peticion = new HttpRequestMessage(HttpMethod.Get, $"api/v1/rpa/lanzador/versiones/{robot.VersionId}/paquete"))
            {
                peticion.Headers.Add(RpaHeaders.SecretoDelLanzador, LeerSecreto());
                using var respuesta = await http.SendAsync(peticion, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                respuesta.EnsureSuccessStatusCode();
                await using var destino = File.Create(zip);
                await respuesta.Content.CopyToAsync(destino, ct).ConfigureAwait(false);
            }

            await using (var leido = File.OpenRead(zip))
            {
                var sha = Convert.ToHexString(await SHA256.HashDataAsync(leido, ct).ConfigureAwait(false));
                if (!sha.Equals(robot.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("lo descargado no coincide con lo que se subió (hash distinto).");
                }
            }

            var contenido = Path.Combine(temporal, "contenido");
            // Refuses any entry that would land outside the folder.
            System.IO.Compression.ZipFile.ExtractToDirectory(zip, contenido);
            DarPermisoDeEjecucion(contenido, robot);
            Directory.Move(contenido, carpeta);
            Directory.Delete(temporal, recursive: true);

            logger.LogInformation("Descargada la versión {Numero} de {Robot}.", robot.Numero, robot.Nombre);
            _avisosPorVersion.Remove(robot.VersionId);
            return dondeCorre;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var aviso = $"No se pudo preparar la versión {robot.Numero} de {robot.Nombre}: {ex.Message} No se arranca hasta que se pueda.";
            if (!_avisosPorVersion.TryGetValue(robot.VersionId, out var anterior) || anterior != aviso) logger.LogError("{Aviso}", aviso);
            _avisosPorVersion[robot.VersionId] = aviso;
            try
            {
                if (Directory.Exists(temporal)) Directory.Delete(temporal, recursive: true);
            }
            catch (IOException)
            {
                // Next round.
            }

            return null;
        }
    }

    // A zip made on Windows or by hand does not say what is executable. What has to be: native helpers for Linux (the browser's
    // driver manager) and a program the robot is run as directly.
    private static void DarPermisoDeEjecucion(string carpeta, RobotDelCatalogoDto robot)
    {
        if (OperatingSystem.IsWindows()) return;

        var ejecutables = new List<string>();
        var runtimes = Path.Combine(carpeta, robot.Raiz, "runtimes");
        if (Directory.Exists(runtimes))
        {
            ejecutables.AddRange(Directory.EnumerateDirectories(runtimes, "linux*")
                .Select(d => Path.Combine(d, "native")).Where(Directory.Exists)
                .SelectMany(d => Directory.EnumerateFiles(d)));
        }

        var programa = Path.GetFullPath(Path.Combine(carpeta, robot.Raiz, robot.Comando));
        if (programa.StartsWith(Path.GetFullPath(carpeta), StringComparison.Ordinal) && File.Exists(programa)) ejecutables.Add(programa);

        foreach (var archivo in ejecutables)
        {
            File.SetUnixFileMode(archivo, File.GetUnixFileMode(archivo) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }
    }

    // Versions no longer in the catalogue, only while nothing runs (a copy may still be running an older one).
    private void Ordenar(HashSet<string> enUso)
    {
        if (!Directory.Exists(donde.CarpetaDeRobots)) return;
        foreach (var carpeta in Directory.EnumerateDirectories(donde.CarpetaDeRobots))
        {
            var nombre = Path.GetFileName(carpeta);
            if (enUso.Contains(nombre) || nombre.EndsWith(".descargando", StringComparison.Ordinal)) continue;
            try
            {
                Directory.Delete(carpeta, recursive: true);
            }
            catch (IOException)
            {
                // Next time.
            }
        }
    }

    private LanzadorOptions Opciones(List<RobotOptions> robots) => new()
    {
        ViriatoUrl = baseDeOpciones.ViriatoUrl,
        IntervaloSegundos = baseDeOpciones.IntervaloSegundos,
        MaxRobotsALaVez = baseDeOpciones.MaxRobotsALaVez,
        MaxConPantalla = baseDeOpciones.MaxConPantalla,
        SalirSinTrabajoSegundos = baseDeOpciones.SalirSinTrabajoSegundos,
        EsperaTrasFalloSegundos = baseDeOpciones.EsperaTrasFalloSegundos,
        GraciaParaSalirSegundos = baseDeOpciones.GraciaParaSalirSegundos,
        ReiniciarSiNoCogeTrabajoSegundos = baseDeOpciones.ReiniciarSiNoCogeTrabajoSegundos,
        GraciaAlPararSegundos = baseDeOpciones.GraciaAlPararSegundos,
        CarpetaTemporal = baseDeOpciones.CarpetaTemporal,
        PantallaBase = baseDeOpciones.PantallaBase,
        PuertoWebBase = baseDeOpciones.PuertoWebBase,
        PuertoVncBase = baseDeOpciones.PuertoVncBase,
        Robots = robots,
    };

    // Said once while it lasts, not every round.
    private void Avisar(string? aviso)
    {
        if (aviso == _ultimoAviso) return;
        if (aviso is not null) logger.LogWarning("{Aviso}", aviso);
        else if (_ultimoAviso is not null) logger.LogInformation("Catálogo de Viriato leído de nuevo.");
        _ultimoAviso = aviso;
    }
}
