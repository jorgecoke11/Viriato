using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Orchestration;
using Viariato.Shared.Authorization;

namespace Viariato.Modules.Casos.AccionesMasivas;

/// <summary>
/// Puts a failed Caso back on its way: reprocesses the step that failed, exactly as the person would from the Caso's own page
/// (<c>Reprocesar</c> in its failed-step notice). The step goes back to the queue with the priority it had. Only failed Casos
/// apply: a cancelled one was cancelled on purpose (by a person or by running out of time) and is not undone here.
/// </summary>
public sealed class ReprocesarCasoAccion(AppDbContext db, IEjecucionOrchestrator orchestrator) : IAccionMasivaSobreCaso
{
    public string Id => "reprocesar";

    public string Permiso => Permissions.CasosManage;

    public string? MotivoPorElQueNoAplica(CasoParaAccion caso) =>
        caso.Estado == CasoEstado.Fallido ? null : "Solo se reprocesan los casos fallidos.";

    public async Task EjecutarAsync(CasoParaAccion caso, JsonElement? parametros, CancellationToken ct)
    {
        // The last step that failed: reprocessing only takes the latest attempt of a step, which is the one that failed last.
        var pasoFallido = await db.Set<EjecucionPaso>().AsNoTracking()
            .Where(p => p.CasoId == caso.Id && p.Estado == EjecucionPasoEstado.Fallido)
            .OrderByDescending(p => p.FinishedAt ?? p.StartedAt)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("No tiene ningún paso fallido que reprocesar.");

        await orchestrator.ReprocesarPasoAsync(pasoFallido, ct);
    }
}
