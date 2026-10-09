using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Viariato.Infrastructure;

namespace Viariato.Modules.Casos.Despacho;

/// <summary>
/// The background sweep that cuts steps that have run past their service's maximum time. It is what makes the cut
/// independent of anyone asking: with no robot polling at all, the Caso of a step that overran is still cancelled within
/// one sweep. With several API replicas only one of them sweeps at a time (the others find the sweep taken and wait for
/// the next tick); and even two sweeping at once would be harmless, since each cut is a single conditional update that only
/// one of them wins.
/// </summary>
public sealed class VigilanteDePasos(IServiceScopeFactory scopes, IOptions<DespachoOptions> opciones, ILogger<VigilanteDePasos> logger) : BackgroundService
{
    /// <summary>Any value will do: it only has to be the same everywhere, and not the dispatcher's.</summary>
    private const long CerrojoDelBarrido = 7_204_602;

    /// <summary>How long a robot copy that stopped asking for work is remembered. Only a tidy-up: past the connection window it
    /// no longer counts for anything.</summary>
    private static readonly TimeSpan OlvidoDeInstancias = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, opciones.Value.BarridoSegundos)));

        try
        {
            while (await temporizador.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await BarrerUnaVezAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A failed sweep must not end the loop: the next one tries again.
                    logger.LogError(ex, "Falló el barrido de tiempos máximos.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The API is shutting down.
        }
    }

    private async Task BarrerUnaVezAsync(CancellationToken ct)
    {
        // A scope per sweep: the control works through the database context, which must not live for ever.
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var libre = await db.Database.SqlQuery<bool>($"select pg_try_advisory_xact_lock({CerrojoDelBarrido}) as \"Value\"").SingleAsync(ct);
        if (!libre) return;

        var control = scope.ServiceProvider.GetRequiredService<ControlDePasos>();
        var cancelados = await control.CancelarVencidosAsync(DateTimeOffset.UtcNow, ct);
        if (cancelados > 0) logger.LogInformation("El barrido canceló {Casos} caso(s) por superar el tiempo máximo de su servicio.", cancelados);

        var limite = DateTimeOffset.UtcNow - OlvidoDeInstancias;
        await db.Database.ExecuteSqlInterpolatedAsync($"delete from rpafleet.despliegue_instancias where last_seen_at < {limite}", ct);

        await transaccion.CommitAsync(ct);
    }
}
