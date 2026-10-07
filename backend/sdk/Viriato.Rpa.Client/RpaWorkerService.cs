using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Viriato.Rpa.Client;

/// <summary>What a robot's process reports back to whoever launched it; read after the host stops.</summary>
internal sealed class RpaRobotEstado
{
    public int CodigoSalida { get; set; }
}

/// <summary>Keeps the robot listening to its Viriato queue until the process is told to stop.</summary>
internal sealed class RpaWorkerService(
    RpaWorker worker,
    IRpaStepHandler handler,
    RpaRobotEstado estado,
    ILogger<RpaWorkerService> logger,
    IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Robot escuchando la cola de Viriato (Ctrl+C para salir).");
            await worker.RunAsync(handler.EjecutarAsync, stoppingToken);
        }
        catch (ViriatoApiException ex) when (ex.IsUnauthorized)
        {
            logger.LogCritical("Viriato rechazó la clave del robot (Viriato:ApiKey). Revisa que sea la de su Despliegue y que no esté regenerada.");
            estado.CodigoSalida = 1;
            lifetime.StopApplication();
        }
    }
}
