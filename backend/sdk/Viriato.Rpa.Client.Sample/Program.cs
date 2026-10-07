using Microsoft.Extensions.Logging;
using Viariato.ApiContracts;
using Viriato.Rpa.Client;

// The smallest robot: everything generic (configuration, logging, the queue loop, retries, Ctrl+C) comes
// from Viriato.Rpa.Client. Run it with the API key of a Despliegue:  Viriato__ApiKey=<clave>  dotnet run
var builder = RpaRobot.CreateBuilder(args);
builder.AddRpaHandler<EjemploHandler>();
return await builder.RunRobotAsync();

// The only part that is specific to a robot.
internal sealed class EjemploHandler(ILogger<EjemploHandler> logger) : IRpaStepHandler
{
    public async Task<RpaStepResult> EjecutarAsync(RpaStepContext paso, CancellationToken ct)
    {
        var ejecucion = paso.Ejecucion;
        logger.LogInformation("Reclamado: caso \"{Caso}\" · aplicación {App} · datos {Datos}", ejecucion.CasoTitulo, ejecucion.AplicacionObjetivo, ejecucion.DatosCasoJson);

        await Task.Delay(TimeSpan.FromSeconds(1), ct);
        await paso.AgregarEvidenciaAsync(EvidenciaTipo.Otro, "Trabajo simulado", contenidoJson: "\"El robot de ejemplo terminó.\"", ct: ct);

        return RpaStepResult.Completado();
    }
}
