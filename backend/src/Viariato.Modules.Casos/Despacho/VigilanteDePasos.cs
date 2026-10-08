using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Viariato.Modules.Casos.Despacho;

/// <summary>
/// The background sweep that cuts steps that have run past their service's maximum time. It is what makes the cut
/// independent of anyone asking: with no robot polling at all, the Caso of a step that overran is still cancelled within
/// one sweep. Safe to run on several API instances at once — each cut is a single conditional update that only one of
/// them wins.
/// </summary>
public sealed class VigilanteDePasos(IServiceScopeFactory scopes, IOptions<DespachoOptions> opciones, ILogger<VigilanteDePasos> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, opciones.Value.BarridoSegundos)));

        try
        {
            while (await temporizador.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    // A scope per sweep: the control works through the database context, which must not live for ever.
                    using var scope = scopes.CreateScope();
                    var control = scope.ServiceProvider.GetRequiredService<ControlDePasos>();
                    var cancelados = await control.CancelarVencidosAsync(DateTimeOffset.UtcNow, stoppingToken);
                    if (cancelados > 0) logger.LogInformation("El barrido canceló {Casos} caso(s) por superar el tiempo máximo de su servicio.", cancelados);
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
}
