using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Infrastructure.BackgroundQueue;
using Viariato.Infrastructure.Trabajos;
using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.Orchestration.Ejecutores;

/// <summary>
/// V1 stub: simulates an outbound API call. A real integration replaces the body of the queued work
/// item with an actual HTTP call using the url/method/headersTemplate from ConfiguracionJson — the
/// response would then live in the Trabajo's Data, same as Markets stores its analysis results.
/// </summary>
public sealed class ApiPasoEjecutor(AppDbContext db, ITrabajoTracker tracker, IBackgroundTaskQueue queue) : IPasoEjecutor
{
    public async Task<PasoResultado> EjecutarAsync(PasoEjecucionContext context, CancellationToken ct)
    {
        var trabajoId = await tracker.StartAsync("casos.paso.api", "ejecucion_paso", context.EjecucionPasoId.ToString(), ct: ct);

        var paso = await db.Set<EjecucionPaso>().FirstAsync(p => p.Id == context.EjecucionPasoId, ct);
        paso.TrabajoId = trabajoId;
        await db.SaveChangesAsync(ct);

        queue.Enqueue(async (workServices, workCt) =>
        {
            var innerTracker = workServices.GetRequiredService<ITrabajoTracker>();
            await innerTracker.RegistrarLogAsync(trabajoId, "Simulando llamada a API externa…", ct: workCt);
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
}
