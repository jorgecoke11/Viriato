using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Despacho;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.RpaFleet.Domain;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

/// <param name="Desde">When the robot claimed it.</param>
/// <param name="LimiteAt">When it must be finished by (its service's maximum time from <paramref name="Desde"/>), or
/// null if the service has no maximum time.</param>
/// <param name="Vencido">It is past that time: it no longer counts as running and the sweep is about to cancel its Caso.</param>
public sealed record EnEjecucionDto(
    Guid CasoId,
    string CasoTitulo,
    Guid ServicioId,
    string ServicioNombre,
    DateTimeOffset Desde,
    int? TiempoMaximoMinutos,
    DateTimeOffset? LimiteAt,
    bool Vencido,
    Guid EjecucionPasoId);

/// <param name="ServicioAlLimiteGlobal">The service has as many steps running as its global cap allows, so this one waits.</param>
public sealed record PendienteDto(
    int Posicion,
    Guid CasoId,
    string CasoTitulo,
    Guid ServicioId,
    string ServicioNombre,
    DateTimeOffset EsperaDesde,
    bool RobotEncendido,
    bool RobotConectado,
    bool RobotOcupado,
    bool ServicioAlLimiteGlobal,
    int Prioridad,
    Guid EjecucionPasoId);

/// <param name="EnUso">Steps running on the machine right now, out of <paramref name="MaxEjecucionesSimultaneas"/>.</param>
/// <param name="MaxEjecucionesSimultaneas">The machine's optional ceiling; null when it has none and its robot copies set the capacity.</param>
/// <param name="Robots">The robots of the machine and how many copies of each are there, and free.</param>
public sealed record ColaEquipoDto(
    int? MaxEjecucionesSimultaneas,
    int EnUso,
    string Politica,
    IReadOnlyList<EnEjecucionDto> EnEjecucion,
    IReadOnlyList<PendienteDto> Pendientes,
    IReadOnlyList<RobotDelEquipoDto> Robots);

public sealed record RobotDelEquipoDto(Guid DespliegueId, Guid ServicioId, string ServicioNombre, bool Encendido, bool Conectado, int Instancias, int Libres);

/// <summary>
/// What a machine is doing and what is waiting for it, in the order it will be served: the answer to "who goes
/// next, and why is this one not moving". Built by the same code that hands out the steps, so it cannot disagree
/// with what actually happens.
/// </summary>
internal static class DespachoColaEndpoints
{
    private const int PendientesPorRobot = 25;

    public static void MapDespachoColaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/equipos/{equipoId:guid}/despacho")
            .RequireAuthorization(Permissions.RpaDespacho)
            .MapGet("/cola", GetColaAsync);
    }

    private static async Task<IResult> GetColaAsync(
        Guid equipoId, AppDbContext db, IOptions<DespachoOptions> opciones, HttpContext http, CancellationToken ct)
    {
        if (!await db.Set<Equipo>().AnyAsync(e => e.Id == equipoId, ct)) return ProblemResults.NotFound(http, "Equipo no encontrado.");

        var ahora = DateTimeOffset.UtcNow;
        var estado = await DespachoEquipo.CargarAsync(db, equipoId, despliegueQueConsulta: null, instanciaQueConsulta: null, ahora, opciones.Value, ct);
        var servicios = await db.Set<Servicio>().AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Nombre, ct);
        string NombreServicio(Guid id) => servicios.GetValueOrDefault(id, string.Empty);

        // Everything claimed and unfinished — including steps past their time that the sweep has not cancelled yet,
        // shown as overdue (they are exactly what someone looking at this screen wants to see).
        var enEjecucion = (await PasoEnMarcha.CargarAsync(db, equipoId, ct))
            .Select(p => new EnEjecucionDto(
                p.CasoId, p.CasoTitulo, p.ServicioId, NombreServicio(p.ServicioId), p.ReclamadoEn, p.TiempoMaximoMinutos, p.LimiteAt, p.Vencido(ahora), p.PasoId))
            .ToList();

        // One queue per robot, each ordered by its executions' priority and then by age — what the robot would be handed.
        var colas = new List<IReadOnlyList<(PendienteDto Base, Candidato Candidato)>>();
        foreach (var robot in estado.Robots.Where(r => r.Despliegue.Encendido))
        {
            var d = robot.Despliegue;
            var filas = await (
                from detalle in db.Set<RpaEjecucionDetalle>().AsNoTracking()
                join paso in db.Set<EjecucionPaso>().AsNoTracking() on detalle.EjecucionPasoId equals paso.Id
                join caso in db.Set<Caso>().AsNoTracking() on paso.CasoId equals caso.Id
                join pasoDef in db.Set<FlujoPasoDef>().AsNoTracking() on paso.FlujoPasoDefId equals pasoDef.Id
                join version in db.Set<FlujoVersion>().AsNoTracking() on pasoDef.FlujoVersionId equals version.Id
                where detalle.DespliegueId == null && paso.Estado == EjecucionPasoEstado.EnProgreso
                    && pasoDef.ServicioId == d.ServicioId && version.FlujoId == d.FlujoId
                orderby detalle.Prioridad descending, detalle.Id
                select new { DetalleId = detalle.Id, PasoId = paso.Id, CasoId = caso.Id, caso.Titulo, detalle.Prioridad, paso.StartedAt, paso.CreatedAt }
            ).Take(PendientesPorRobot).ToListAsync(ct);

            var alLimite = estado.ServiciosAlLimite.Contains(d.ServicioId);
            colas.Add(filas.Select(fila =>
            (
                new PendienteDto(
                    0, fila.CasoId, fila.Titulo, d.ServicioId, NombreServicio(d.ServicioId),
                    fila.StartedAt ?? fila.CreatedAt, d.Encendido, robot.Conectado, robot.Ocupado, alLimite, fila.Prioridad, fila.PasoId),
                new Candidato(d.Id, d.ServicioId, fila.DetalleId, fila.StartedAt ?? fila.CreatedAt)
            )).ToList());
        }

        var pendientes = Despachador
            .Servir(colas, e => e.Candidato, estado.Orden, estado.Equipo.Politica, estado.UltimoServicioId)
            .Select((e, i) => e.Base with { Posicion = i + 1 })
            .ToList();

        return Results.Ok(new ColaEquipoDto(
            estado.Equipo.MaxEjecucionesSimultaneas, estado.EnEjecucion, estado.Equipo.Politica.ToString(), enEjecucion, pendientes,
            estado.Robots
                .OrderBy(r => NombreServicio(r.Despliegue.ServicioId))
                .Select(r => new RobotDelEquipoDto(
                    r.Despliegue.Id, r.Despliegue.ServicioId, NombreServicio(r.Despliegue.ServicioId), r.Despliegue.Encendido, r.Conectado, r.Instancias, r.Libres))
                .ToList()));
    }
}
