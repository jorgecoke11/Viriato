using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Infrastructure.BackgroundQueue;
using Viariato.Infrastructure.Trabajos;
using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.Orchestration.Ejecutores;

/// <summary>
/// V1 stub: simulates an RPA robot run instead of driving a real one. What matters architecturally —
/// starting a Trabajo, dispatching through IBackgroundTaskQueue instead of blocking the request, and
/// calling back into the orchestrator once done — is real; only the "robot" itself is fake. A real
/// integration replaces the body of the queued work item with an actual robot invocation.
/// </summary>
public sealed class RpaPasoEjecutor(AppDbContext db, ITrabajoTracker tracker, IBackgroundTaskQueue queue) : IPasoEjecutor
{
    public async Task<PasoResultado> EjecutarAsync(PasoEjecucionContext context, CancellationToken ct)
    {
        var aplicacion = ExtraerAplicacion(context.PasoDef.ConfiguracionJson);

        var trabajoId = await tracker.StartAsync("casos.paso.rpa", "ejecucion_paso", context.EjecucionPasoId.ToString(), ct: ct);

        var paso = await db.Set<EjecucionPaso>().FirstAsync(p => p.Id == context.EjecucionPasoId, ct);
        paso.TrabajoId = trabajoId;
        db.Add(new RpaEjecucionDetalle { EjecucionPasoId = paso.Id, AplicacionObjetivo = aplicacion });
        await db.SaveChangesAsync(ct);

        queue.Enqueue(async (workServices, workCt) =>
        {
            var innerTracker = workServices.GetRequiredService<ITrabajoTracker>();
            await innerTracker.RegistrarLogAsync(trabajoId, $"Simulando ejecución RPA sobre {aplicacion}…", ct: workCt);
            await Task.Delay(TimeSpan.FromMilliseconds(300), workCt);
            await innerTracker.CompleteAsync(trabajoId, summary: "Simulado", ct: workCt);

            var innerDb = workServices.GetRequiredService<AppDbContext>();
            var innerPaso = await innerDb.Set<EjecucionPaso>().FirstAsync(p => p.Id == context.EjecucionPasoId, workCt);
            innerPaso.Estado = EjecucionPasoEstado.Completado;
            innerPaso.FinishedAt = DateTimeOffset.UtcNow;
            await innerDb.SaveChangesAsync(workCt);

            var orchestrator = workServices.GetRequiredService<IEjecucionOrchestrator>();
            await orchestrator.AvanzarAsync(context.EjecucionPasoId, workCt);
        });

        return PasoResultado.EnProgreso;
    }

    private static string ExtraerAplicacion(string? configuracionJson)
    {
        if (string.IsNullOrWhiteSpace(configuracionJson)) return "desconocida";
        using var doc = JsonDocument.Parse(configuracionJson);
        return doc.RootElement.TryGetProperty("aplicacion", out var valor) ? valor.GetString() ?? "desconocida" : "desconocida";
    }
}
