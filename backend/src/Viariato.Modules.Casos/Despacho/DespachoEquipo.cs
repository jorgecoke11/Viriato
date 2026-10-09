using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.Casos.Despacho;

/// <summary>A robot (Despliegue) of the machine, with what the dispatcher needs to know about its running copies right now.</summary>
/// <param name="Conectado">Has called the API recently (an idle robot polls every few seconds).</param>
/// <param name="Instancias">How many copies of it are there: asking for work within the connection window, or holding a step.</param>
/// <param name="Libres">Of those, how many hold no step in execution and so could take one. Zero = the robot is busy.</param>
internal sealed record RobotDelEquipo(Despliegue Despliegue, bool Conectado, int Instancias, int Libres)
{
    public bool Ocupado => Libres == 0;
}

/// <summary>Everything the dispatcher needs to decide for one machine, read in one go.</summary>
/// <param name="EnEjecucion">Steps running on the machine: claimed, unfinished and still within their service's maximum time.</param>
/// <param name="Candidatos">What could be handed out next, one per robot with a free copy, already without anything held
/// back by a service's global cap.</param>
/// <param name="ServiciosAlLimite">Services whose global cap is full right now, across all machines.</param>
/// <param name="ConsultanteOcupado">The copy that is asking already holds a step in execution, so it must not be given another.</param>
internal sealed record EstadoDespacho(
    Equipo Equipo,
    IReadOnlyList<Guid> Orden,
    IReadOnlyList<RobotDelEquipo> Robots,
    int EnEjecucion,
    IReadOnlyList<Candidato> Candidatos,
    Guid? UltimoServicioId,
    IReadOnlySet<Guid> ServiciosAlLimite,
    bool ConsultanteOcupado);

/// <summary>
/// Reads the state of a machine for <see cref="Despachador"/>. Who is working is read from the steps themselves, not
/// guessed from signals the robots send:
/// <list type="bullet">
/// <item>A step <b>is running</b> if it is claimed, unfinished and has not passed its service's maximum time (see
/// <see cref="PasoEnMarcha"/>). One that has passed it no longer counts — whether or not the sweep has cancelled its
/// Caso yet — so a copy that hangs never holds anything longer than that time.</item>
/// <item>A robot may run as <b>several copies</b> (<see cref="InstanciaDeDespliegue"/>), each one slot: a copy is busy if
/// it holds a running step, and free if it has asked for work recently and holds none. A robot with a free copy
/// competes for the next step; a stuck copy only takes its own slot. A robot that never says which copy it is counts as
/// one copy, so it behaves as it always did.</item>
/// <item>A service with a <b>global cap</b> contributes no candidate while that many of its steps are running
/// anywhere.</item>
/// </list>
/// </summary>
internal static class DespachoEquipo
{
    public static async Task<EstadoDespacho> CargarAsync(
        AppDbContext db,
        Guid equipoId,
        Guid? despliegueQueConsulta,
        string? instanciaQueConsulta,
        DateTimeOffset ahora,
        DespachoOptions opciones,
        CancellationToken ct)
    {
        var equipo = await db.Set<Equipo>().AsNoTracking().FirstAsync(e => e.Id == equipoId, ct);

        var orden = await db.Set<EquipoServicioOrden>().AsNoTracking()
            .Where(o => o.EquipoId == equipoId).OrderBy(o => o.Orden)
            .Select(o => o.ServicioId).ToListAsync(ct);

        var despliegues = await db.Set<Despliegue>().AsNoTracking().Where(d => d.EquipoId == equipoId).ToListAsync(ct);
        var despliegueIds = despliegues.Select(d => d.Id).ToList();
        var limiteConexion = ahora - opciones.VentanaConexion;
        var consulta = instanciaQueConsulta ?? InstanciaDeDespliegue.SinId;
        bool ConectadoPorUso(Despliegue d) => d.Id == despliegueQueConsulta || (d.LastUsedAt is { } t && t >= limiteConexion);

        var enMarcha = (await PasoEnMarcha.CargarAsync(db, equipoId: null, ct)).Where(p => !p.Vencido(ahora)).ToList();
        var deEstaMaquina = enMarcha.Where(p => p.EquipoId == equipoId).ToList();
        var ocupadas = deEstaMaquina.Select(p => (p.DespliegueId, Instancia: p.InstanciaId ?? InstanciaDeDespliegue.SinId)).ToHashSet();

        var vistas = await db.Set<InstanciaDeDespliegue>().AsNoTracking()
            .Where(i => despliegueIds.Contains(i.DespliegueId) && i.LastSeenAt >= limiteConexion)
            .Select(i => new { i.DespliegueId, i.InstanciaId })
            .ToListAsync(ct);
        var conectadas = vistas.Select(i => (i.DespliegueId, Instancia: i.InstanciaId)).ToHashSet();

        // The copy that is asking is there by definition, whether or not it has been written down yet.
        if (despliegueQueConsulta is { } consultante) conectadas.Add((consultante, consulta));

        // A robot that only shows up as "used recently" (it called the API but never asked for work with a copy id — an
        // older client) counts as its one anonymous copy, unless it is clearly running as identified copies.
        foreach (var d in despliegues)
        {
            var identificado = conectadas.Any(c => c.DespliegueId == d.Id && c.Instancia != InstanciaDeDespliegue.SinId)
                || ocupadas.Any(o => o.DespliegueId == d.Id && o.Instancia != InstanciaDeDespliegue.SinId);
            if (!identificado && ConectadoPorUso(d)) conectadas.Add((d.Id, InstanciaDeDespliegue.SinId));
        }

        var robots = despliegues.Select(d =>
        {
            var suyas = conectadas.Where(c => c.DespliegueId == d.Id).Select(c => c.Instancia)
                .Concat(ocupadas.Where(o => o.DespliegueId == d.Id).Select(o => o.Instancia))
                .ToHashSet();
            var libres = conectadas.Count(c => c.DespliegueId == d.Id && !ocupadas.Contains(c));
            return new RobotDelEquipo(d, ConectadoPorUso(d) || suyas.Count > 0, suyas.Count, libres);
        }).ToList();

        var serviciosAlLimite = await ServiciosAlLimiteGlobalAsync(db, enMarcha, ct);

        var candidatos = new List<Candidato>();
        foreach (var robot in robots.Where(r => r.Despliegue.Encendido && r.Libres > 0))
        {
            if (serviciosAlLimite.Contains(robot.Despliegue.ServicioId)) continue;

            var siguiente = await SiguienteDelRobotAsync(db, robot.Despliegue, ct);
            if (siguiente is not null) candidatos.Add(siguiente);
        }

        var ultimoServicioId = equipo.Politica == PoliticaDespacho.Turnos
            ? await UltimoServicioServidoAsync(db, equipoId, despliegues.ToDictionary(d => d.Id), ct)
            : null;

        var consultanteOcupado = despliegueQueConsulta is { } id && ocupadas.Contains((id, consulta));

        return new EstadoDespacho(equipo, orden, robots, deEstaMaquina.Count, candidatos, ultimoServicioId, serviciosAlLimite, consultanteOcupado);
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

    /// <summary>What goes first in the queue of this robot (its service, in its process), if anything is waiting: the
    /// execution with the highest priority, and among equals the one that has waited longest.</summary>
    public static async Task<Candidato?> SiguienteDelRobotAsync(AppDbContext db, Despliegue robot, CancellationToken ct)
    {
        var fila = await (
            from detalle in db.Set<RpaEjecucionDetalle>().AsNoTracking()
            join paso in db.Set<EjecucionPaso>().AsNoTracking() on detalle.EjecucionPasoId equals paso.Id
            join pasoDef in db.Set<FlujoPasoDef>().AsNoTracking() on paso.FlujoPasoDefId equals pasoDef.Id
            join version in db.Set<FlujoVersion>().AsNoTracking() on pasoDef.FlujoVersionId equals version.Id
            where detalle.DespliegueId == null && paso.Estado == EjecucionPasoEstado.EnProgreso
                && pasoDef.ServicioId == robot.ServicioId && version.FlujoId == robot.FlujoId
            orderby detalle.Prioridad descending, detalle.Id
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
