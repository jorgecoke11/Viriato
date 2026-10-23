using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Conexiones;
using Viariato.Modules.Conexiones.Tipos;
using Viariato.Modules.Notificaciones.Domain;

namespace Viariato.Modules.Notificaciones.Envio;

/// <summary>Sends what is waiting in the outbox. Public and with its own clock so the tests can run one turn by hand.</summary>
public sealed class BandejaDeSalida(AppDbContext db, EnviadorDeMensajes enviador, ILogger<BandejaDeSalida> logger)
{
    /// <summary>Any value will do: the same on every replica, and no other sweep's.</summary>
    private const long CerrojoDeLaBandeja = 7_204_604;

    /// <summary>Wait before the 2nd, 3rd, 4th and 5th attempt.</summary>
    private static readonly TimeSpan[] Esperas = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1)];

    public async Task<int> UnaVueltaAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        // Only one replica sends at a time (the lock lasts as long as this transaction), so no notice goes out twice.
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var libre = await db.Database.SqlQuery<bool>($"select pg_try_advisory_xact_lock({CerrojoDeLaBandeja}) as \"Value\"").SingleAsync(ct);
        if (!libre) return 0;

        var pendientes = await db.Set<EnvioDeNotificacion>()
            .Where(e => e.Estado == EstadoDeEnvio.Pendiente && e.ProximoIntentoAt <= ahora)
            .OrderBy(e => e.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        foreach (var envio in pendientes)
        {
            envio.Intentos++;
            try
            {
                await enviador.EnviarAsync(envio.ConexionId, new Mensaje(envio.Destinatarios, envio.Asunto, envio.Cuerpo), ct);
                envio.Estado = EstadoDeEnvio.Enviado;
                envio.EnviadoAt = ahora;
                envio.UltimoError = null;
                Anotar(envio, CasoEventoAccion.NotificacionEnviada, null, ahora);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var motivo = ex is ConexionException ? ex.Message : $"Error inesperado: {ex.Message}";
                envio.UltimoError = motivo.Length > 1000 ? motivo[..1000] : motivo;
                if (envio.Intentos >= EnvioDeNotificacion.MaximoDeIntentos)
                {
                    envio.Estado = EstadoDeEnvio.Fallido;
                    Anotar(envio, CasoEventoAccion.NotificacionFallida, envio.UltimoError, ahora);
                }
                else
                {
                    envio.ProximoIntentoAt = ahora + Esperas[envio.Intentos - 1];
                }
                logger.LogWarning(ex, "No se pudo enviar el aviso {Envio} (intento {Intento}).", envio.Id, envio.Intentos);
            }

            await db.SaveChangesAsync(ct);
        }

        await transaccion.CommitAsync(ct);
        return pendientes.Count;
    }

    /// <summary>A line in the case's history: who was told what, or that it could not be done.</summary>
    private void Anotar(EnvioDeNotificacion envio, CasoEventoAccion accion, string? error, DateTimeOffset ahora) =>
        db.Add(new CasoEvento
        {
            CasoId = envio.CasoId,
            Accion = accion,
            DetalleJson = error is null
                ? JsonSerializer.Serialize(new { regla = envio.ReglaNombre, destinatarios = envio.Destinatarios, asunto = envio.Asunto })
                : JsonSerializer.Serialize(new { regla = envio.ReglaNombre, destinatarios = envio.Destinatarios, asunto = envio.Asunto, error }),
            OccurredAt = ahora,
        });
}

/// <summary>The postman: every few seconds, one turn of the outbox.</summary>
internal sealed class CarteroDeNotificaciones(IServiceScopeFactory scopes, IOptions<NotificacionesOptions> opciones, ILogger<CarteroDeNotificaciones> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, opciones.Value.SegundosEntreRondas)));
        try
        {
            while (await temporizador.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<BandejaDeSalida>().UnaVueltaAsync(DateTimeOffset.UtcNow, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A failed turn must not end the loop: the next one tries again.
                    logger.LogError(ex, "Falló la vuelta del cartero de notificaciones.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The API is shutting down.
        }
    }
}
