using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.Casos.Despacho;

/// <summary>A robot of the machine, with what the dispatcher needs to know about it right now.</summary>
/// <param name="Conectado">Has called the API recently (an idle robot polls every few seconds).</param>
/// <param name="Ocupado">Holds a step that is in execution (claimed, unfinished, within its service's maximum time).</param>
internal sealed record RobotDelEquipo(Despliegue Despliegue, bool Conectado, bool Ocupado);

/// <summary>Everything the dispatcher needs to decide for one machine, read in one go.</summary>
/// <param name="EnEjecucion">Steps running on the machine: claimed, unfinished and still within their service's maximum time.</param>
/// <param name="Candidatos">What could be handed out next, one per idle robot, already without anything held back by
/// a service's global cap.</param>
/// <param name="ServiciosAlLimite">Services whose global cap is full right now, across all machines.</param>
internal sealed record EstadoDespacho(
    Equipo Equipo,
    IReadOnlyList<Guid> Orden,
    IReadOnlyList<RobotDelEquipo> Robots,
    int EnEjecucion,
    IReadOnlyList<Candidato> Candidatos,
    Guid? UltimoServicioId,
    IReadOnlySet<Guid> ServiciosAlLimite);

/// <summary>
/// Reads the state of a machine for <see cref="Despachador"/>. Who is working is read from the steps themselves, not
/// guessed from signals the robots send:
/// <list type="bullet">
/// <item>A step <b>is running</b> if it is claimed, unfinished and has not passed its service's maximum time (see
/// <see cref="PasoEnMarcha"/>). One that has passed it no longer counts — whether or not the sweep has cancelled its
/// Caso yet — so a robot that hangs never holds a slot longer than that time.</item>
/// <item>A robot is <b>busy</b> if it holds such a step; only idle, switched-on robots that called the API recently
/// compete for the next one. One that is off, or gone for longer than the connection window, holds nobody up.</item>
/// <item>A service with a <b>global cap</b> contributes no candidate while that many of its steps are running
/// anywhere.</item>
/// </list>
/// </summary>
internal static class DespachoEquipo
{
    public static async Task<EstadoDespacho> CargarAsync(
        AppDbContext db, Guid equipoId, Guid? despliegueQueConsulta, DateTimeOffset ahora, DespachoOptions opciones, CancellationToken ct)
    {
        var equipo = await db.Set<Equipo>().AsNoTracking().FirstAsync(e => e.Id == equipoId, ct);

        var orden = await db.Set<EquipoServicioOrden>().AsNoTracking()
            .Where(o => o.EquipoId == equipoId).OrderBy(o => o.Orden)
            .Select(o => o.ServicioId).ToListAsync(ct);

        var despliegues = await db.Set<Despliegue>().AsNoTracking().Where(d => d.EquipoId == equipoId).ToListAsync(ct);
        var limiteConexion = ahora - opciones.VentanaConexion;
        bool Conectado(Despliegue d) => d.Id == despliegueQueConsulta || (d.LastUsedAt is { } t && t >= limiteConexion);

        var enMarcha = (await PasoEnMarcha.CargarAsync(db, equipoId: null, ct)).Where(p => !p.Vencido(ahora)).ToList();
        var deEstaMaquina = enMarcha.Where(p => p.EquipoId == equipoId).ToList();
        var ocupadosSet = deEstaMaquina.Select(p => p.DespliegueId).ToHashSet();

        var robots = despliegues.Select(d => new RobotDelEquipo(d, Conectado(d), ocupadosSet.Contains(d.Id))).ToList();

        var serviciosAlLimite = await ServiciosAlLimiteGlobalAsync(db, enMarcha, ct);

        var candidatos = new List<Candidato>();
        foreach (var robot in robots.Where(r => r.Despliegue.Encendido && r.Conectado && !r.Ocupado))
        {
            if (serviciosAlLimite.Contains(robot.Despliegue.ServicioId)) continue;

            var siguiente = await SiguienteDelRobotAsync(db, robot.Despliegue, ct);
            if (siguiente is not null) candidatos.Add(siguiente);
        }

        var ultimoServicioId = equipo.Politica == PoliticaDespacho.Turnos
            ? await UltimoServicioServidoAsync(db, equipoId, despliegues.ToDictionary(d => d.Id), ct)
            : null;

        return new EstadoDespacho(equipo, orden, robots, deEstaMaquina.Count, candidatos, ultimoServicioId, serviciosAlLimite);
    }

    /// <summary>The services whose global cap is full: that many of their steps are running right now, on any machine.</summary>
    public static async Task<HashSet<Guid>> ServiciosAlLimiteGlobalAsync(
        AppDbContext db, IReadOnlyList<PasoEnMarcha> enMarcha, CancellationToken ct)
    {
        var conLimite = await db.Set<Servicio>().AsNoTracking()
            .Where(s => s.MaxEjecucionesGlobales != null)
            .ToDictionaryAsync(s => s.Id, s => s.MaxEjecucionesGlobales!.Value, ct);
        if (conLimite.Count == 0) return [];

        return enMarcha
            .GroupBy(p => p.ServicioId)
            .Where(g => conLimite.TryGetValue(g.Key, out var tope) && g.Count() >= tope)
            .Select(g => g.Key)
            .ToHashSet();
    }

    /// <summary>The oldest unclaimed step in the queue of this robot (its service, in its process), if any.</summary>
    public static async Task<Candidato?> SiguienteDelRobotAsync(AppDbContext db, Despliegue robot, CancellationToken ct)
    {
        var fila = await (
            from detalle in db.Set<RpaEjecucionDetalle>().AsNoTracking()
            join paso in db.Set<EjecucionPaso>().AsNoTracking() on detalle.EjecucionPasoId equals paso.Id
            join pasoDef in db.Set<FlujoPasoDef>().AsNoTracking() on paso.FlujoPasoDefId equals pasoDef.Id
            join version in db.Set<FlujoVersion>().AsNoTracking() on pasoDef.FlujoVersionId equals version.Id
            where detalle.DespliegueId == null && paso.Estado == EjecucionPasoEstado.EnProgreso
                && pasoDef.ServicioId == robot.ServicioId && version.FlujoId == robot.FlujoId
            orderby detalle.Id
            select new { detalle.Id, paso.StartedAt, paso.CreatedAt }
        ).FirstOrDefaultAsync(ct);

        return fila is null
            ? null
            : new Candidato(robot.Id, robot.ServicioId, fila.Id, fila.StartedAt ?? fila.CreatedAt);
    }

    private static async Task<Guid?> UltimoServicioServidoAsync(
        AppDbContext db, Guid equipoId, Dictionary<Guid, Despliegue> porId, CancellationToken ct)
    {
        var ultimo = await db.Set<RpaEjecucionDetalle>().AsNoTracking()
            .Where(d => d.DespliegueId != null && d.ClaimedAt != null
                && db.Set<Despliegue>().Any(x => x.Id == d.DespliegueId && x.EquipoId == equipoId))
            .OrderByDescending(d => d.ClaimedAt)
            .Select(d => d.DespliegueId)
            .FirstOrDefaultAsync(ct);

        return ultimo is { } despliegueId && porId.TryGetValue(despliegueId, out var despliegue) ? despliegue.ServicioId : null;
    }
}
