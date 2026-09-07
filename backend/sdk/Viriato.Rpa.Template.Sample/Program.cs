using Viriato.Rpa.Template;

// Minimal end-to-end smoke test for Viriato.Rpa.Template: point it at a running Api and a real
// Despliegue's API key, and it polls, claims, "does the work" (just logs + a short delay here), and
// reports back exactly the way a real robot would.
var baseUrl = Environment.GetEnvironmentVariable("VIRIATO_API_URL") ?? "http://localhost:8080";
var apiKey = Environment.GetEnvironmentVariable("VIRIATO_API_KEY")
    ?? throw new InvalidOperationException("Set VIRIATO_API_KEY to a Despliegue's API key before running the sample.");
var equipoId = Environment.GetEnvironmentVariable("VIRIATO_EQUIPO_ID");

using var client = new RpaClient(new RpaClientOptions { BaseUrl = baseUrl, ApiKey = apiKey, EquipoId = equipoId });

Console.WriteLine($"Conectando a {baseUrl}…");
var estado = await client.ObtenerEstadoAsync();
Console.WriteLine($"Despliegue: equipo={estado.EquipoNombre}, servicio={estado.ServicioNombre}, flujo={estado.FlujoNombre}, encendido={estado.Encendido}");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine("Escuchando la cola (Ctrl+C para salir)…");
await client.EjecutarAsync(
    async (ejecucion, _, ct) =>
    {
        Console.WriteLine($"Reclamado: caso \"{ejecucion.CasoTitulo}\" · aplicación {ejecucion.AplicacionObjetivo} · paso {ejecucion.EjecucionPasoId}");
        await Task.Delay(TimeSpan.FromSeconds(1), ct);
        Console.WriteLine("Trabajo simulado completado.");
        return null;
    },
    intervaloPolling: TimeSpan.FromSeconds(3),
    ct: cts.Token);
