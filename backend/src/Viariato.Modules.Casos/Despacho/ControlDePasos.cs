using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Viariato.Infrastructure;
using Viariato.Infrastructure.Trabajos;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Orchestration;

namespace Viariato.Modules.Casos.Despacho;

/// <summary>
/// What the platform does with a claimed step that has run past its service's maximum time: the Caso is cancelled (the
/// step as Cancelado with the reason, then the Caso the way a user cancelling it would). The robot is never asked
/// anything — the time is the only rule. The cut is one conditional update on the step, so it cannot race with the robot
/// reporting the step done at the same moment: whoever gets there first wins, and the other finds the step already
/// settled (a robot that reports late is turned down with a 409).
/// </summary>
public sealed class ControlDePasos(
    AppDbContext db,
    ITrabajoTracker tracker,
    IEjecucionOrchestrator orchestrator,
    ILogger<ControlDePasos> logger)
{
    /// <summary>Cancels the Caso of every step that has run past its service's maximum time. Returns how many.</summary>
    public async Task<int> CancelarVencidosAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        var vencidos = (await PasoEnMarcha.CargarAsync(db, equipoId: null, ct)).Where(p => p.Vencido(ahora)).ToList();

        var cancelados = 0;
        foreach (var paso in vencidos)
        {
            if (await CancelarPorTiempoAsync(paso, ct)) cancelados++;
        }

        return cancelados;
    }

    internal async Task<bool> CancelarPorTiempoAsync(PasoEnMarcha paso, CancellationToken ct)
    {
        var mensaje =
            $"El paso superó el tiempo máximo de su servicio ({paso.TiempoMaximoMinutos} min) y la plataforma canceló el caso. "
            + "Si es normal que tarde más, aumenta el tiempo máximo del servicio.";

        var cerrado = await CerrarSiSigueEnCursoAsync(paso.PasoId, mensaje, ct);
        if (!cerrado) return false;

        logger.LogWarning(
            "Caso {Caso}: el paso {Paso} superó los {Minutos} min de su servicio; se cancela el caso.",
            paso.CasoId, paso.PasoId, paso.TiempoMaximoMinutos);

        await FallarTrabajoAsync(paso.PasoId, mensaje, ct);
        await orchestrator.CancelarAsync(paso.CasoId, ct);
        return true;
    }

    private async Task<bool> CerrarSiSigueEnCursoAsync(Guid pasoId, string mensaje, CancellationToken ct)
    {
        var ahora = DateTimeOffset.UtcNow;
        var filas = await db.Set<EjecucionPaso>()
            .Where(p => p.Id == pasoId && p.Estado == EjecucionPasoEstado.EnProgreso)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Estado, EjecucionPasoEstado.Cancelado)
                .SetProperty(p => p.ErrorMensaje, mensaje)
                .SetProperty(p => p.FinishedAt, ahora), ct);
        return filas > 0;
    }

    private async Task FallarTrabajoAsync(Guid pasoId, string mensaje, CancellationToken ct)
    {
        var trabajoId = await db.Set<EjecucionPaso>().AsNoTracking().Where(p => p.Id == pasoId).Select(p => p.TrabajoId).FirstAsync(ct);
        if (trabajoId is { } id)
        {
            await tracker.FailAsync(id, mensaje, ct: ct);
        }
    }
}
