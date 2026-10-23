using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Orchestration;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Casos.Reprocesamientos;

/// <summary>
/// Where reprocessing rules fire. A case's end always leaves a line in its history (Completado, Fallido or Cancelado), whoever caused it,
/// so this looks at the history lines about to be saved — like the notification rules do — and, for a case that ended, applies the first
/// active rule of its process that matches: it schedules the reprocessing, or, if the rule's limit has run out, leaves the case on the
/// rule's final business state. In the same save as the end: a case and what was decided about it are saved together or not at all.
/// The running itself is the reprocessor's (<see cref="ReprocesadorDeCasos"/>), apart, when the moment comes.
/// </summary>
internal sealed class DisparadorDeReprocesamientos : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } db) await AplicarAsync(db, cancellationToken);
        return result;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } db) AplicarAsync(db, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    private static async Task AplicarAsync(DbContext db, CancellationToken ct)
    {
        var finales = db.ChangeTracker.Entries<CasoEvento>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .Select(e => (Evento: e, Estado: ComoAcaboElPaso(e.Accion)))
            .Where(f => f.Estado is not null)
            .ToList();

        foreach (var (evento, estadoDelPaso) in finales)
        {
            // The case is tracked by whoever ended it; the rule may change its business state.
            var caso = db.ChangeTracker.Entries<Caso>().Select(e => e.Entity).FirstOrDefault(c => c.Id == evento.CasoId)
                ?? await db.Set<Caso>().FirstOrDefaultAsync(c => c.Id == evento.CasoId, ct);
            if (caso?.EjecucionActualId is null) continue;

            // A person who discards a case means it: no rule brings it back.
            var codigoDeNegocio = await CodigoDelEstadoAsync(db, caso.EstadoNegocioActualId, ct);
            if (codigoDeNegocio == EstadosDeSistema.Descartado) continue;

            var regla = (await db.Set<ReglaDeReprocesamiento>().AsNoTracking()
                    .Where(r => r.Activa && r.FlujoId == caso.FlujoId)
                    .OrderBy(r => r.Orden).ThenBy(r => r.CreatedAt)
                    .ToListAsync(ct))
                .FirstOrDefault(r =>
                    (r.EstadoNegocioId is null || r.EstadoNegocioId == caso.EstadoNegocioActualId)
                    && (r.EstadoEjecucion is null || r.EstadoEjecucion == estadoDelPaso));
            if (regla is null) continue;

            var hechos = await db.Set<ReprocesamientoProgramado>().CountAsync(p => p.CasoId == caso.Id && p.ReglaId == regla.Id && p.Estado != EstadoDeReprocesamiento.Descartado, ct)
                + db.ChangeTracker.Entries<ReprocesamientoProgramado>().Count(e => e.State == EntityState.Added && e.Entity.CasoId == caso.Id && e.Entity.ReglaId == regla.Id);

            var ahora = DateTimeOffset.UtcNow;
            if (regla.Limite is { } limite && hechos >= limite)
            {
                await AgotarAsync(db, caso, evento, regla, limite, ahora, ct);
                continue;
            }

            var programadoPara = ahora.AddMinutes(regla.EsperaMinutos);
            db.Add(new ReprocesamientoProgramado
            {
                FlujoId = caso.FlujoId,
                ReglaId = regla.Id,
                ReglaNombre = regla.Nombre,
                CasoId = caso.Id,
                PasoNombre = regla.PasoNombre,
                Numero = hechos + 1,
                ProgramadoPara = programadoPara,
                CreatedAt = ahora,
            });
            db.Add(new CasoEvento
            {
                CasoId = caso.Id,
                EjecucionId = evento.EjecucionId,
                Accion = CasoEventoAccion.ReprocesamientoProgramado,
                DetalleJson = JsonSerializer.Serialize(new
                {
                    regla = regla.Nombre, numero = hechos + 1, limite = regla.Limite, paso = regla.PasoNombre, programadoPara,
                }),
                OccurredAt = ahora,
            });
        }
    }

    /// <summary>How the case's last step ended, read from the line that ends the case; null for any other line.</summary>
    private static EjecucionPasoEstado? ComoAcaboElPaso(CasoEventoAccion accion) => accion switch
    {
        CasoEventoAccion.Completado => EjecucionPasoEstado.Completado,
        CasoEventoAccion.Fallido => EjecucionPasoEstado.Fallido,
        CasoEventoAccion.Cancelado => EjecucionPasoEstado.Cancelado,
        _ => null,
    };

    private static async Task<string?> CodigoDelEstadoAsync(DbContext db, Guid? estadoId, CancellationToken ct)
    {
        if (estadoId is not { } id) return null;
        // The engine may have just provisioned it in this same save.
        return db.ChangeTracker.Entries<FlujoEstadoDef>().Select(e => e.Entity).FirstOrDefault(e => e.Id == id)?.Codigo
            ?? await db.Set<FlujoEstadoDef>().AsNoTracking().Where(e => e.Id == id).Select(e => e.Codigo).FirstOrDefaultAsync(ct);
    }

    private static async Task AgotarAsync(
        DbContext db, Caso caso, CasoEvento evento, ReglaDeReprocesamiento regla, int limite, DateTimeOffset ahora, CancellationToken ct)
    {
        db.Add(new CasoEvento
        {
            CasoId = caso.Id,
            EjecucionId = evento.EjecucionId,
            Accion = CasoEventoAccion.ReprocesamientosAgotados,
            DetalleJson = JsonSerializer.Serialize(new { regla = regla.Nombre, limite }),
            OccurredAt = ahora,
        });

        if (regla.EstadoFinalId is not { } finalId || finalId == caso.EstadoNegocioActualId) return;
        var estado = await db.Set<FlujoEstadoDef>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == finalId, ct);
        if (estado is null) return;

        caso.EstadoNegocioActualId = estado.Id;
        caso.UpdatedAt = ahora;
        // A history line like any other change of business state, so the notification rules listening for it fire too.
        db.Add(new CasoEvento
        {
            CasoId = caso.Id,
            EjecucionId = evento.EjecucionId,
            Accion = CasoEventoAccion.EstadoNegocioActualizado,
            DetalleJson = JsonSerializer.Serialize(new { estado.Codigo, estado.Display }),
            OccurredAt = ahora,
        });
    }
}
