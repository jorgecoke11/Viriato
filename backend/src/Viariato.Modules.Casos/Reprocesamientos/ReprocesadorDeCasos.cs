using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Despacho;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Orchestration;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Casos.Reprocesamientos;

/// <summary>
/// Runs the reprocessings whose moment has come: the step goes back to work exactly as with the case's own «Reprocesar» button
/// (<see cref="IEjecucionOrchestrator.ReprocesarPasoAsync"/>), keeping the priority it had in the queue. Each one is taken with a
/// conditional update, so with several API replicas only one of them runs it.
/// </summary>
public sealed class ReprocesadorDeCasos(AppDbContext db, IEjecucionOrchestrator orchestrator, ILogger<ReprocesadorDeCasos> logger)
{
    /// <summary>One look at the clock. Public so it can be tested with a clock of its own. Returns how many it ran.</summary>
    public async Task<int> UnaVueltaAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        var debidos = await db.Set<ReprocesamientoProgramado>().AsNoTracking()
            .Where(p => p.Estado == EstadoDeReprocesamiento.Pendiente && p.ProgramadoPara <= ahora)
            .OrderBy(p => p.ProgramadoPara)
            .Take(50)
            .ToListAsync(ct);

        var hechos = 0;
        foreach (var programado in debidos)
        {
            if (await TomarAsync(programado.Id, ct) && await ReprocesarAsync(programado, ct)) hechos++;
        }

        return hechos;
    }

    private async Task<bool> TomarAsync(Guid id, CancellationToken ct)
    {
        var ahora = DateTimeOffset.UtcNow;
        var filas = await db.Set<ReprocesamientoProgramado>()
            .Where(p => p.Id == id && p.Estado == EstadoDeReprocesamiento.Pendiente)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Estado, EstadoDeReprocesamiento.Hecho).SetProperty(p => p.HechoAt, ahora), ct);
        return filas > 0;
    }

    private async Task<bool> ReprocesarAsync(ReprocesamientoProgramado programado, CancellationToken ct)
    {
        var caso = await db.Set<Caso>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == programado.CasoId, ct);
        if (caso?.EjecucionActualId is not { } ejecucionId) return false;

        if (caso.Estado is not (CasoEstado.Completado or CasoEstado.Fallido or CasoEstado.Cancelado))
        {
            // Somebody put it back to work in the meantime.
            await CerrarAsync(programado.Id, EstadoDeReprocesamiento.Descartado, "El caso ya estaba otra vez en marcha.", ct);
            return false;
        }

        Guid pasoId;
        try
        {
            pasoId = await PasoAReprocesarAsync(caso, ejecucionId, programado.PasoNombre, ct);
            await orchestrator.ReprocesarPasoAsync(pasoId, ct);
        }
        catch (InvalidOperationException ex)
        {
            db.ChangeTracker.Clear();
            await CerrarAsync(programado.Id, EstadoDeReprocesamiento.Fallido, ex.Message, ct);
            db.Add(new CasoEvento
            {
                CasoId = caso.Id,
                EjecucionId = ejecucionId,
                Accion = CasoEventoAccion.ReprocesamientoFallido,
                DetalleJson = JsonSerializer.Serialize(new { regla = programado.ReglaNombre, paso = programado.PasoNombre, error = ex.Message }),
                OccurredAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Caso {Caso}: no se pudo reprocesar por la regla «{Regla}»: {Error}", caso.Id, programado.ReglaNombre, ex.Message);
            return false;
        }

        logger.LogInformation("Caso {Caso}: reprocesado por la regla «{Regla}» ({Numero}).", caso.Id, programado.ReglaNombre, programado.Numero);
        return true;
    }

    /// <summary>The latest attempt, in the case's execution, of the step named (or of the step it ended on).</summary>
    private async Task<Guid> PasoAReprocesarAsync(Caso caso, Guid ejecucionId, string? pasoNombre, CancellationToken ct)
    {
        if (pasoNombre is null)
        {
            return await db.Set<Ejecucion>().AsNoTracking().Where(e => e.Id == ejecucionId).Select(e => e.PasoActualId).FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("El caso no tiene ningún paso que reprocesar.");
        }

        var pasoDefId = await db.Set<FlujoPasoDef>().AsNoTracking()
            .Where(p => p.FlujoVersionId == caso.FlujoVersionId && p.Nombre == pasoNombre)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"La versión del proceso de este caso no tiene el paso «{pasoNombre}».");

        return await db.Set<EjecucionPaso>().AsNoTracking()
            .Where(p => p.EjecucionId == ejecucionId && p.FlujoPasoDefId == pasoDefId && p.Estado != EjecucionPasoEstado.Omitido)
            .OrderByDescending(p => p.NumeroIntento)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"El caso no llegó al paso «{pasoNombre}».");
    }

    private Task CerrarAsync(Guid id, EstadoDeReprocesamiento estado, string error, CancellationToken ct) =>
        db.Set<ReprocesamientoProgramado>().Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Estado, estado).SetProperty(p => p.Error, error), ct);
}

/// <summary>The reprocessor's clock: every few seconds, a turn in a scope of its own.</summary>
internal sealed class VigilanteDeReprocesamientos(IServiceScopeFactory scopes, IOptions<DespachoOptions> opciones, ILogger<VigilanteDeReprocesamientos> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, opciones.Value.ReprocesamientosSegundos)));
        try
        {
            while (await temporizador.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<ReprocesadorDeCasos>().UnaVueltaAsync(DateTimeOffset.UtcNow, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A failed turn must not end the loop: the next one tries again.
                    logger.LogError(ex, "Falló la vuelta de los reprocesamientos.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The API is shutting down.
        }
    }
}
