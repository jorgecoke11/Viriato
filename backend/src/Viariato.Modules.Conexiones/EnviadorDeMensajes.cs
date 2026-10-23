using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Conexiones.Domain;
using Viariato.Modules.Conexiones.Tipos;
using Viariato.Modules.RpaFleet.Security;

namespace Viariato.Modules.Conexiones;

/// <summary>Sends a message through one of the platform's accounts — what other modules (notifications) use, without knowing
/// what kind of account it is or how its password is kept.</summary>
public sealed class EnviadorDeMensajes(AppDbContext db, TiposDeConexion tipos, ICredencialProtector protector)
{
    /// <exception cref="ConexionException">The account does not exist, is switched off, cannot send, or the sending failed.</exception>
    public async Task EnviarAsync(Guid conexionId, Mensaje mensaje, CancellationToken ct)
    {
        var conexion = await db.Set<Conexion>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == conexionId, ct)
            ?? throw new ConexionException("La conexión ya no existe.");
        await EnviarAsync(conexion, mensaje, ct);
    }

    internal async Task EnviarAsync(Conexion conexion, Mensaje mensaje, CancellationToken ct)
    {
        if (!conexion.Activa) throw new ConexionException($"La conexión «{conexion.Nombre}» está desactivada.");
        if (tipos.Buscar(conexion.Tipo) is not ITipoQueEnviaMensajes tipo)
        {
            throw new ConexionException($"La conexión «{conexion.Nombre}» no sabe enviar mensajes.");
        }

        string? secreto;
        try
        {
            secreto = conexion.SecretoCifrado is null ? null : protector.Revelar(conexion.SecretoCifrado, conexion.Id);
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            throw new ConexionException($"No se puede leer la contraseña de «{conexion.Nombre}» (¿cambió la clave de cifrado?): vuelve a escribirla.", ex);
        }

        using var configuracion = JsonDocument.Parse(conexion.ConfiguracionJson);
        await tipo.EnviarAsync(configuracion.RootElement, secreto, mensaje, ct);
    }
}
