using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.Casos.Despacho;

/// <summary>
/// A step a robot has claimed and not finished: it is <b>in execution</b>. Whether it is still allowed to be is a
/// matter of its service's maximum time, counted from the moment it was claimed — nothing else: no heartbeat, no
/// guess about the robot. Past that time the step no longer counts as running (so its machine's slot is free) and the
/// sweep cancels its Caso.
/// </summary>
internal sealed record PasoEnMarcha(
    Guid PasoId,
    Guid DetalleId,
    Guid CasoId,
    string CasoTitulo,
    Guid DespliegueId,
    Guid ServicioId,
    Guid EquipoId,
    DateTimeOffset ReclamadoEn,
    int? TiempoMaximoMinutos,
    string? InstanciaId = null)
{
    /// <summary>When the step must be finished by, or null if its service has no maximum time.</summary>
    public DateTimeOffset? LimiteAt => TiempoMaximoMinutos is { } minutos ? ReclamadoEn.AddMinutes(minutos) : null;

    public bool Vencido(DateTimeOffset ahora) => LimiteAt is { } limite && limite <= ahora;

    /// <summary>Every claimed, unfinished step (of one machine, or of all of them), oldest first. There are never many:
    /// at most one per robot per slot of its machine.</summary>
    public static async Task<List<PasoEnMarcha>> CargarAsync(AppDbContext db, Guid? equipoId, CancellationToken ct)
    {
        var filas = await (
            from detalle in db.Set<RpaEjecucionDetalle>().AsNoTracking()
            join paso in db.Set<EjecucionPaso>().AsNoTracking() on detalle.EjecucionPasoId equals paso.Id
            join caso in db.Set<Caso>().AsNoTracking() on paso.CasoId equals caso.Id
            join despliegue in db.Set<Despliegue>().AsNoTracking() on detalle.DespliegueId equals despliegue.Id
            join servicio in db.Set<Servicio>().AsNoTracking() on despliegue.ServicioId equals servicio.Id
            where paso.Estado == EjecucionPasoEstado.EnProgreso && (equipoId == null || despliegue.EquipoId == equipoId)
            select new
            {
                PasoId = paso.Id,
                DetalleId = detalle.Id,
                CasoId = caso.Id,
                caso.Titulo,
                DespliegueId = despliegue.Id,
                despliegue.ServicioId,
                despliegue.EquipoId,
                detalle.ClaimedAt,
                detalle.InstanciaId,
                paso.StartedAt,
                paso.CreatedAt,
                servicio.TiempoMaximoMinutos,
            }
        ).ToListAsync(ct);

        return filas
            .Select(f => new PasoEnMarcha(
                f.PasoId, f.DetalleId, f.CasoId, f.Titulo, f.DespliegueId, f.ServicioId, f.EquipoId,
                f.ClaimedAt ?? f.StartedAt ?? f.CreatedAt, f.TiempoMaximoMinutos, f.InstanciaId))
            .OrderBy(p => p.ReclamadoEn)
            .ToList();
    }
}
