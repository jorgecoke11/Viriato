using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Viariato.ApiContracts;
using Viariato.Shared.Seguridad;

namespace Viariato.Modules.RpaFleet.Lanzador;

/// <summary>
/// The secret Viriato shares with the launcher next to it, and the passes made with it.
///
/// The secret lives in a file (<see cref="LanzadorOptions.ArchivoSecreto"/>) on a volume only the Api and the launcher mount: it
/// is created here the first time, so nobody types or copies it. With it the launcher reads its catalogue and downloads robots.
///
/// A pass is what a robot started by the launcher uses as its key: a <see cref="PaseFirmado"/> (<c>rpl_{despliegue}.{caduca}.{firma}</c>)
/// made with the secret. Nothing is stored: it is checked by signing it again. It stops working when it expires, when the Despliegue
/// is deleted, or when the secret changes (delete the file and restart the Api to void every pass at once).
/// </summary>
public sealed class SecretoDelLanzador(IOptions<LanzadorOptions> opciones, TimeProvider reloj)
{
    private readonly object _cerrojo = new();
    private byte[]? _secreto;

    /// <summary>Whether there is a launcher next to this Viriato at all.</summary>
    public bool Disponible => !string.IsNullOrWhiteSpace(opciones.Value.ArchivoSecreto);

    /// <summary>Reads the secret, creating it the first time. Throws if there is no launcher configured.</summary>
    public byte[] Secreto()
    {
        if (!Disponible) throw new InvalidOperationException("No hay lanzador junto a Viriato (Lanzador:ArchivoSecreto está vacío).");

        lock (_cerrojo)
        {
            if (_secreto is not null) return _secreto;

            var ruta = Path.GetFullPath(opciones.Value.ArchivoSecreto);
            if (File.Exists(ruta) && File.ReadAllText(ruta).Trim() is { Length: > 0 } guardado)
            {
                _secreto = Encoding.UTF8.GetBytes(guardado);
                return _secreto;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            var nuevo = Base64Url(RandomNumberGenerator.GetBytes(32));
            // Written aside and moved, so the launcher never reads half a secret.
            var temporal = ruta + ".nuevo";
            File.WriteAllText(temporal, nuevo);
            if (!OperatingSystem.IsWindows())
            {
                // The launcher runs as another user: it has to be able to read it. Nobody else is in either container.
                File.SetUnixFileMode(temporal, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            }

            File.Move(temporal, ruta, overwrite: true);
            _secreto = Encoding.UTF8.GetBytes(nuevo);
            return _secreto;
        }
    }

    /// <summary>Whether what the launcher sent is the secret. Constant time.</summary>
    public bool Coincide(string? enviado) =>
        Disponible && !string.IsNullOrEmpty(enviado)
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(enviado), Secreto());

    /// <summary>A pass for a robot of this Despliegue, valid for <see cref="LanzadorOptions.DiasDelPase"/> days.</summary>
    public string EmitirPase(Guid despliegueId) =>
        PaseFirmado.Emitir(Secreto(), Proposito, despliegueId, reloj.GetUtcNow().AddDays(Math.Max(1, opciones.Value.DiasDelPase)), RpaHeaders.PrefijoDePase);

    /// <summary>The Despliegue a pass belongs to, or null if it is not a valid pass (forged, expired, or of another secret).</summary>
    public Guid? ValidarPase(string pase) =>
        Disponible ? PaseFirmado.Validar(Secreto(), Proposito, pase, reloj.GetUtcNow(), RpaHeaders.PrefijoDePase) : null;

    private const string Proposito = "viriato:robot-del-lanzador";

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}

/// <summary>Creates the secret when the Api starts, so the launcher (which waits for the file) can begin.</summary>
internal sealed class CrearSecretoDelLanzador(SecretoDelLanzador secreto, Microsoft.Extensions.Logging.ILogger<CrearSecretoDelLanzador> logger)
    : Microsoft.Extensions.Hosting.IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!secreto.Disponible) return Task.CompletedTask;
        try
        {
            secreto.Secreto();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not fatal for Viriato: only the launcher depends on it, and it says so.
            logger.LogError(ex, "No se pudo crear el secreto del lanzador; los robots del lanzador no arrancarán.");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
