using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Infrastructure.BackgroundQueue;
using Viariato.Infrastructure.Trabajos;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Casos.Orchestration.Ejecutores;

/// <summary>
/// V1 stub: simulates an agent run instead of actually calling a model. What matters
/// architecturally — starting a Trabajo, dispatching asynchronously, and recording a structured
/// AgenteEjecucionDetalle — is real; only the "agent" itself is fake. Dynamically created sub-agents
/// aren't modeled here explicitly: they'd simply be further Trabajo rows nested under this step's
/// TrabajoId via Trabajo.ParentTrabajoId, exactly like Markets nests one child Trabajo per company.
/// </summary>
public sealed class AgentePasoEjecutor(AppDbContext db, ITrabajoTracker tracker, IBackgroundTaskQueue queue) : IPasoEjecutor
{
    public async Task<PasoResultado> EjecutarAsync(PasoEjecucionContext context, CancellationToken ct)
    {
        var modelo = "desconocido";
        if (context.PasoDef.AgenteDefinicionId is { } agenteId)
        {
            var agente = await db.Set<AgenteDefinicion>().AsNoTracking().FirstOrDefaultAsync(a => a.Id == agenteId, ct);
            modelo = agente?.Modelo ?? modelo;
        }

        var trabajoId = await tracker.StartAsync("casos.paso.agente", "ejecucion_paso", context.EjecucionPasoId.ToString(), ct: ct);

        var paso = await db.Set<EjecucionPaso>().FirstAsync(p => p.Id == context.EjecucionPasoId, ct);
        paso.TrabajoId = trabajoId;
        db.Add(new AgenteEjecucionDetalle { EjecucionPasoId = paso.Id, Modelo = modelo });
        await db.SaveChangesAsync(ct);

        queue.Enqueue(async (workServices, workCt) =>
        {
            var innerTracker = workServices.GetRequiredService<ITrabajoTracker>();
            await innerTracker.RegistrarLogAsync(trabajoId, $"Simulando ejecución del agente ({modelo})…", ct: workCt);
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
