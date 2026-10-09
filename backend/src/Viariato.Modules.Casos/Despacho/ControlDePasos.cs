using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Viariato.Infrastructure;
using Viariato.Infrastructure.Trabajos;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Orchestration;

namespace Viariato.Modules.Casos.Despacho;

/// <summary>
/// What the platform does with a claimed step that has run past its service's maximum time: the Caso is cancelled (the
/// step as Cancelado with the reason, then the Caso — which ends on the business estado «Cancelado por exceso de tiempo de
/// ejecución», not on «Descartado», so it is plain at a glance that a person did not do it). The robot is never asked
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

        var cancelada = await CancelarAsync(paso.PasoId, paso.CasoId, mensaje, MotivoDeCancelacion.TiempoMaximo, ct);
        if (cancelada)
        {
            logger.LogWarning(
                "Caso {Caso}: el paso {Paso} superó los {Minutos} min de su servicio; se cancela el caso.",
                paso.CasoId, paso.PasoId, paso.TiempoMaximoMinutos);
        }

        return cancelada;
    }

    /// <summary>
    /// A person cancels an execution — an RPA step that waits in the queue or that a robot is running. Its step is left as
    /// cancelled, with the reason, and so is its Caso: the steps of a Caso run one after the other, so with this one gone
    /// the Caso has nowhere to go. A robot that reports on it afterwards is turned down (the step is already settled).
    /// False if it was no longer waiting or running — it finished, failed or was cancelled in the meantime.
    /// </summary>
    public async Task<bool> CancelarEjecucionAsync(Guid pasoId, Guid casoId, CancellationToken ct)
    {
        const string mensaje = "La ejecución se canceló manualmente y, con ella, el caso.";

        var cancelada = await CancelarAsync(pasoId, casoId, mensaje, MotivoDeCancelacion.Manual, ct);
        if (cancelada) logger.LogInformation("Caso {Caso}: la ejecución {Paso} se canceló manualmente; se cancela el caso.", casoId, pasoId);
        return cancelada;
    }

    private async Task<bool> CancelarAsync(Guid pasoId, Guid casoId, string mensaje, MotivoDeCancelacion motivo, CancellationToken ct)
    {
        if (!await CerrarSiSigueEnCursoAsync(pasoId, mensaje, ct)) return false;

        await FallarTrabajoAsync(pasoId, mensaje, ct);
        await orchestrator.CancelarAsync(casoId, ct, motivo);
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
