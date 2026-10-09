using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Viariato.ApiContracts;
using Viariato.Infrastructure;
using Viariato.Infrastructure.Trabajos;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Orchestration;
using Viariato.Modules.Casos.Storage;
using Viariato.Modules.Casos.Validation;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.RpaFleet.Auth;
using Viariato.Modules.RpaFleet.Domain;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>
/// The surface an external robot (Viriato.Rpa.Client) actually calls — ApiKey-scheme only, never
/// the user Bearer scheme. Every write is scoped to one EjecucionPasoId the caller's Despliegue
/// legitimately claimed off the queue (see RpaWorkerAuthorization), so unlike every other Casos
/// endpoint this never needs the AsignacionFlujo boundary check.
/// </summary>
internal static class RpaWorkerEndpoints
{
    public static void MapRpaWorkerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/rpa")
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(ApiKeyDefaults.AuthenticationScheme)
                .RequireClaim(ApiKeyDefaults.DespliegueIdClaimType));

        group.MapGet("/despliegue", GetDespliegueAsync);
        group.MapGet("/parametros", GetParametrosAsync);
        group.MapPost("/cola/siguiente", ClaimSiguienteAsync);
        group.MapPost("/casos", CrearCasoAsync);
        group.MapPost("/pasos/{id:guid}/completar", CompletarAsync);
        group.MapPost("/pasos/{id:guid}/completar-caso", CompletarCasoAsync);
        group.MapPost("/pasos/{id:guid}/fallar", FallarAsync);
        group.MapPost("/pasos/{id:guid}/evidencias", AgregarEvidenciaAsync).DisableAntiforgery();
        group.MapPost("/pasos/{id:guid}/estado-negocio", CambiarEstadoNegocioAsync);
        group.MapPost("/pasos/{id:guid}/en-vivo", ReportarEnVivoAsync);
    }

    private static async Task<IResult> GetDespliegueAsync(ClaimsPrincipal principal, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var despliegue = await db.Set<Despliegue>().AsNoTracking()
            .Include(d => d.Equipo).Include(d => d.Servicio)
            .FirstOrDefaultAsync(d => d.Id == principal.GetDespliegueId(), ct);
        if (despliegue is null) return ProblemResults.NotFound(http, "Despliegue no encontrado.");

        var flujoNombre = await db.Set<Flujo>().AsNoTracking()
            .Where(f => f.Id == despliegue.FlujoId).Select(f => f.Nombre).FirstOrDefaultAsync(ct) ?? string.Empty;

        return Results.Ok(new DespliegueEstadoDto(
            despliegue.Encendido, despliegue.Equipo?.Nombre ?? string.Empty, despliegue.Servicio?.Nombre ?? string.Empty, flujoNombre));
    }

    /// <summary>The settings of the process this Despliegue belongs to, as a code → value map. Plain text and
    /// not secret (see FlujoParametro); the process comes from the API key itself, so a robot can never ask
    /// for another process's settings.</summary>
    private static async Task<IResult> GetParametrosAsync(ClaimsPrincipal principal, AppDbContext db, CancellationToken ct)
    {
        var flujoId = principal.GetFlujoId();

        var parametros = await db.Set<FlujoParametro>().AsNoTracking()
            .Where(p => p.FlujoId == flujoId)
            .ToDictionaryAsync(p => p.Codigo, p => p.Valor, ct);

        return Results.Ok(parametros);
    }

    /// <summary>Lets a "launcher" robot open Cases. The process is never taken from the request: it is the
    /// one an admin set as this Despliegue's FlujoDestino, so a leaked key can only ever add Cases to that
    /// one process (and none at all if the Despliegue was not given one). The Case starts on the process's
    /// active published version, exactly as if a user had created it, and is attributed to no user.</summary>
    private static async Task<IResult> CrearCasoAsync(
        CrearCasoRobotRequest request,
        CrearCasoRobotRequestValidator validator,
        ClaimsPrincipal principal,
        AppDbContext db,
        IEjecucionOrchestrator orchestrator,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var despliegue = await db.Set<Despliegue>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == principal.GetDespliegueId(), ct);
        if (despliegue is null) return ProblemResults.NotFound(http, "Despliegue no encontrado.");
        if (!despliegue.Encendido) return ProblemResults.Conflict(http, "El despliegue está apagado.");
        if (despliegue.FlujoDestinoId is not { } flujoDestinoId)
        {
            return ProblemResults.Forbidden(http, "Este despliegue no tiene permiso para crear casos. Indica en Despliegues en qué proceso puede hacerlo.");
        }

        var flujo = await db.Set<Flujo>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == flujoDestinoId, ct);
        if (flujo is null) return ProblemResults.Conflict(http, "El proceso en el que este despliegue puede crear casos ya no existe.");
        if (!flujo.Activo) return ProblemResults.Conflict(http, $"El proceso \"{flujo.Nombre}\" está desactivado.");
        if (flujo.VersionActivaId is not { } versionId)
        {
            return ProblemResults.Conflict(http, $"El proceso \"{flujo.Nombre}\" no tiene una versión publicada.");
        }

        var version = await db.Set<FlujoVersion>().AsNoTracking().FirstAsync(v => v.Id == versionId, ct);
        if (version.Estado != FlujoVersionEstado.Publicada)
        {
            return ProblemResults.Conflict(http, "La versión activa del proceso no está publicada.");
        }

        FlujoTipoCasoDef? tipo = null;
        if (!string.IsNullOrWhiteSpace(request.TipoCaso))
        {
            var nombre = request.TipoCaso.Trim().ToLower();
            tipo = await db.Set<FlujoTipoCasoDef>().AsNoTracking()
                .FirstOrDefaultAsync(t => t.FlujoId == flujoDestinoId && t.Activo && t.Nombre.ToLower() == nombre, ct);
            if (tipo is null)
            {
                return ProblemResults.Conflict(http, $"El proceso \"{flujo.Nombre}\" no tiene el tipo de caso \"{request.TipoCaso}\".");
            }
        }

        if (DatosPorTipo.Rechazar(tipo?.EsquemaDatosJson, request.DatosJson) is { } datosRechazados) return datosRechazados;

        FlujoEstadoDef? estado = null;
        if (!string.IsNullOrWhiteSpace(request.EstadoNegocioCodigo))
        {
            var codigo = request.EstadoNegocioCodigo.Trim();
            estado = await db.Set<FlujoEstadoDef>().AsNoTracking()
                .FirstOrDefaultAsync(e => e.FlujoId == flujoDestinoId && e.Activo && e.Codigo == codigo, ct);
            if (estado is null)
            {
                return ProblemResults.Conflict(http, $"El proceso \"{flujo.Nombre}\" no tiene el estado de negocio \"{request.EstadoNegocioCodigo}\".");
            }
        }

        var pasos = await db.Set<FlujoPasoDef>().AsNoTracking()
            .Where(p => p.FlujoVersionId == version.Id).OrderBy(p => p.Orden).ToListAsync(ct);
        var pasoInicial = pasos.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(request.PasoInicial))
        {
            var nombrePaso = request.PasoInicial.Trim();
            pasoInicial = pasos.FirstOrDefault(p => p.Nombre.Equals(nombrePaso, StringComparison.OrdinalIgnoreCase));
            if (pasoInicial is null)
            {
                return ProblemResults.Conflict(http, $"El proceso \"{flujo.Nombre}\" no tiene el paso \"{request.PasoInicial}\".");
            }
        }

        // A robot that opens Cases starting at its own step would feed itself forever.
        if (pasoInicial?.ServicioId == despliegue.ServicioId)
        {
            return ProblemResults.Conflict(http,
                $"El caso empezaría por el paso \"{pasoInicial.Nombre}\", que es de este mismo servicio. Indica un paso inicial posterior (PasoInicial).");
        }

        var now = DateTimeOffset.UtcNow;
        var caso = new Caso
        {
            FlujoId = flujoDestinoId,
            FlujoVersionId = version.Id,
            Titulo = request.Titulo.Trim(),
            DatosJson = string.IsNullOrWhiteSpace(request.DatosJson) ? null : request.DatosJson,
            TipoCasoId = tipo?.Id,
            EstadoNegocioActualId = estado?.Id,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Add(caso);
        db.Add(new CasoEvento
        {
            CasoId = caso.Id,
            Accion = CasoEventoAccion.Creado,
            DetalleJson = JsonSerializer.Serialize(new { creadoPorDespliegueId = despliegue.Id }),
            OccurredAt = now,
        });
        if (estado is not null)
        {
            db.Add(new CasoEvento
            {
                CasoId = caso.Id,
                Accion = CasoEventoAccion.EstadoNegocioActualizado,
                DetalleJson = JsonSerializer.Serialize(new { estado.Codigo, estado.Display }),
                OccurredAt = now,
            });
        }
        await db.SaveChangesAsync(ct);

        try
        {
            await orchestrator.IniciarCasoAsync(caso.Id, pasoInicial?.Id, ct, request.Prioridad);
        }
        catch (InvalidOperationException ex)
        {
            return ProblemResults.Conflict(http, ex.Message);
        }

        return Results.Ok(new CasoCreadoDto(caso.Id, caso.Titulo));
    }

    /// <summary>Any value will do: it only has to be the same everywhere. Held while a claim is decided.</summary>
    private const long CerrojoDeDespacho = 7_204_601;

    /// <summary>
    /// Hands the next step to the copy of the robot that asks. A copy only asks when it is idle, so every copy that asks
    /// is a free slot: capacity is however many copies the stack runs, and one that is stuck on a step never holds the
    /// others back. What it is handed is the front of its robot's queue — the execution with the highest priority, and among
    /// equals the one that has waited longest — decided under one lock, so copies asking at the same moment receive the
    /// queue strictly in that order.
    ///
    /// Two things can still answer "not you, not yet" (204, the same as an empty queue — the copy asks again in a few
    /// seconds): a service capped across machines that is at its limit, and a machine that has an optional ceiling on steps
    /// at once. Under such a ceiling the robots of the machine take turns in the machine's order of services (see
    /// <see cref="Despacho.Despachador"/>). A step handed out may stay in execution as long as its service's maximum time
    /// allows; past that the platform cancels its Caso (see <see cref="Despacho.ControlDePasos"/>).
    /// </summary>
    private static async Task<IResult> ClaimSiguienteAsync(
        ClaimsPrincipal principal,
        AppDbContext db,
        IOptions<Despacho.DespachoOptions> opciones,
        HttpContext http,
        CancellationToken ct)
    {
        var despliegueId = principal.GetDespliegueId();

        var despliegue = await db.Set<Despliegue>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == despliegueId, ct);
        if (despliegue is null) return ProblemResults.NotFound(http, "Despliegue no encontrado.");
        if (!despliegue.Encendido) return ProblemResults.Conflict(http, "El despliegue está apagado.");

        var config = opciones.Value;
        var ahora = DateTimeOffset.UtcNow;
        var instanciaId = LeerInstancia(http);

        // One decision at a time for the whole platform: a service's global cap spans machines, and nothing narrower
        // than a platform-wide lock keeps two machines from both taking its last slot. A decision takes milliseconds.
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"select pg_advisory_xact_lock({CerrojoDeDespacho})", ct);

        // This copy is there, and free unless it already holds a step: written down so the other robots of the machine know
        // it exists. It lives in the same transaction, so it is only kept if the decision is.
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            insert into rpafleet.despliegue_instancias (despliegue_id, instancia_id, last_seen_at)
            values ({despliegueId}, {instanciaId}, {ahora})
            on conflict (despliegue_id, instancia_id) do update set last_seen_at = excluded.last_seen_at", ct);

        // Optimistic claim on top of that, for a step taken by something that does not take the lock: if it was
        // taken first (0 rows affected), look again.
        for (var intento = 0; intento < 5; intento++)
        {
            var estado = await Despacho.DespachoEquipo.CargarAsync(db, despliegue.EquipoId, despliegueId, instanciaId, ahora, config, ct);

            // A copy that asks while it still holds a step (it restarted with the same id, or it is not waiting its turn) gets nothing.
            if (estado.ConsultanteOcupado)
            {
                await transaccion.CommitAsync(ct);
                return Results.NoContent();
            }

            var tope = estado.Equipo.MaxEjecucionesSimultaneas;
            if (tope is { } maximo && estado.EnEjecucion >= maximo)
            {
                await transaccion.CommitAsync(ct);
                return Results.NoContent();
            }

            // No ceiling: every robot works its own queue, so this copy takes the front of its robot's. With one, the robots
            // of the machine take turns, in the machine's order.
            var elegido = tope is null
                ? estado.Candidatos.FirstOrDefault(c => c.DespliegueId == despliegueId)
                : Despacho.Despachador.Elegir(estado.Candidatos, estado.Orden, estado.Equipo.Politica, estado.UltimoServicioId);
            if (elegido is null || elegido.DespliegueId != despliegueId)
            {
                await transaccion.CommitAsync(ct);
                return Results.NoContent();
            }

            var reclamadoEn = DateTimeOffset.UtcNow;

            var filasActualizadas = await db.Set<RpaEjecucionDetalle>()
                .Where(d => d.Id == elegido.DetalleId && d.DespliegueId == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.DespliegueId, despliegueId)
                    .SetProperty(d => d.ClaimedAt, reclamadoEn)
                    .SetProperty(d => d.InstanciaId, instanciaId == InstanciaDeDespliegue.SinId ? null : instanciaId), ct);

            if (filasActualizadas == 0)
            {
                continue;
            }

            var asignado = await (
                from detalle in db.Set<RpaEjecucionDetalle>().AsNoTracking()
                join paso in db.Set<EjecucionPaso>().AsNoTracking() on detalle.EjecucionPasoId equals paso.Id
                join caso in db.Set<Caso>().AsNoTracking() on paso.CasoId equals caso.Id
                join pasoDef in db.Set<FlujoPasoDef>().AsNoTracking() on paso.FlujoPasoDefId equals pasoDef.Id
                join flujoVersion in db.Set<FlujoVersion>().AsNoTracking() on pasoDef.FlujoVersionId equals flujoVersion.Id
                join flujo in db.Set<Flujo>().AsNoTracking() on flujoVersion.FlujoId equals flujo.Id
                where detalle.Id == elegido.DetalleId
                select new EjecucionAsignadaDto(
                    paso.Id, caso.Id, caso.Titulo, flujo.Nombre, detalle.AplicacionObjetivo, detalle.ParametrosEntrada, caso.DatosJson,
                    caso.TipoCaso == null ? null : caso.TipoCaso.Nombre)
            ).FirstAsync(ct);

            // The step is being run now: the Caso stops being pending. Conditional, so a Caso that was paused or cancelled
            // in the meantime keeps that state.
            var ahoraReclamado = DateTimeOffset.UtcNow;
            await db.Set<Caso>()
                .Where(c => c.Id == asignado.CasoId && c.Estado == CasoEstado.Pendiente)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Estado, CasoEstado.EnProgreso).SetProperty(c => c.UpdatedAt, ahoraReclamado), ct);

            await transaccion.CommitAsync(ct);
            return Results.Ok(asignado);
        }

        await transaccion.CommitAsync(ct);
        return ProblemResults.Conflict(http, "No se pudo reservar un elemento de la cola; inténtalo de nuevo.");
    }

    /// <summary>Which copy of the robot is asking: the id it sends (see <see cref="RpaHeaders.Instancia"/>), cleaned up, or the
    /// empty id of a robot that sends none.</summary>
    private static string LeerInstancia(HttpContext http)
    {
        var valor = http.Request.Headers[RpaHeaders.Instancia].ToString().Trim();
        if (valor.Length == 0) return InstanciaDeDespliegue.SinId;
        return new string(valor.Where(c => !char.IsControl(c)).Take(InstanciaDeDespliegue.LongitudMaxima).ToArray());
    }

    /// <summary>The one conditional update every way of settling a step goes through: it changes the step only if it
    /// is still in progress, so a robot reporting at the very moment the platform cancels the Caso (or two reports at
    /// once) cannot both win. False means somebody else settled it first.</summary>
    private static async Task<bool> CerrarPasoAsync(
        AppDbContext db, Guid pasoId, Guid detalleId, EjecucionPasoEstado nuevoEstado, string? error, string? parametrosSalida, CancellationToken ct)
    {
        var ahora = DateTimeOffset.UtcNow;
        var filas = await db.Set<EjecucionPaso>()
            .Where(p => p.Id == pasoId && p.Estado == EjecucionPasoEstado.EnProgreso)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Estado, nuevoEstado)
                .SetProperty(p => p.ErrorMensaje, error)
                .SetProperty(p => p.FinishedAt, ahora), ct);
        if (filas == 0) return false;

        await db.Set<RpaEjecucionDetalle>().Where(d => d.Id == detalleId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.ParametrosSalida, parametrosSalida), ct);
        return true;
    }

    private static async Task<IResult> CompletarAsync(
        Guid id,
        CompletarPasoRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        ITrabajoTracker tracker,
        IEjecucionOrchestrator orchestrator,
        HttpContext http,
        CancellationToken ct)
    {
        var detalle = await RpaWorkerAuthorization.RequireClaimedStepAsync(db, principal.GetDespliegueId(), id, ct);
        if (detalle is null) return ProblemResults.NotFound(http, "Paso no encontrado.");

        var trabajoId = await db.Set<EjecucionPaso>().AsNoTracking().Where(p => p.Id == id).Select(p => p.TrabajoId).FirstAsync(ct);
        if (!await CerrarPasoAsync(db, id, detalle.Id, EjecucionPasoEstado.Completado, null, request.ParametrosSalida, ct))
        {
            return ProblemResults.Conflict(http, "Este paso ya no está en progreso.");
        }

        if (trabajoId is { } trabajo)
        {
            await tracker.CompleteAsync(trabajo, summary: "Completado por el robot.", ct: ct);
        }

        await orchestrator.AvanzarAsync(id, ct);

        return Results.NoContent();
    }

    /// <summary>For the one robot that knows it is the true end of the cycle: completes this paso AND
    /// closes the whole Caso right now, skipping the normal Orden-position walk — so it finishes the
    /// Caso even if the Flujo has more steps defined after this one.</summary>
    private static async Task<IResult> CompletarCasoAsync(
        Guid id,
        CompletarPasoRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        ITrabajoTracker tracker,
        IEjecucionOrchestrator orchestrator,
        HttpContext http,
        CancellationToken ct)
    {
        var detalle = await RpaWorkerAuthorization.RequireClaimedStepAsync(db, principal.GetDespliegueId(), id, ct);
        if (detalle is null) return ProblemResults.NotFound(http, "Paso no encontrado.");

        var trabajoId = await db.Set<EjecucionPaso>().AsNoTracking().Where(p => p.Id == id).Select(p => p.TrabajoId).FirstAsync(ct);
        if (!await CerrarPasoAsync(db, id, detalle.Id, EjecucionPasoEstado.Completado, null, request.ParametrosSalida, ct))
        {
            return ProblemResults.Conflict(http, "Este paso ya no está en progreso.");
        }

        if (trabajoId is { } trabajo)
        {
            await tracker.CompleteAsync(trabajo, summary: "Completado por el robot.", ct: ct);
        }

        await orchestrator.CompletarCasoAsync(id, ct);

        return Results.NoContent();
    }

    private static async Task<IResult> FallarAsync(
        Guid id,
        FallarPasoRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        ITrabajoTracker tracker,
        IEjecucionOrchestrator orchestrator,
        HttpContext http,
        CancellationToken ct)
    {
        var detalle = await RpaWorkerAuthorization.RequireClaimedStepAsync(db, principal.GetDespliegueId(), id, ct);
        if (detalle is null) return ProblemResults.NotFound(http, "Paso no encontrado.");

        var trabajoId = await db.Set<EjecucionPaso>().AsNoTracking().Where(p => p.Id == id).Select(p => p.TrabajoId).FirstAsync(ct);
        if (!await CerrarPasoAsync(db, id, detalle.Id, EjecucionPasoEstado.Fallido, request.Error, null, ct))
        {
            return ProblemResults.Conflict(http, "Este paso ya no está en progreso.");
        }

        if (trabajoId is { } trabajo)
        {
            await tracker.FailAsync(trabajo, request.Error, ct: ct);
        }

        await orchestrator.AvanzarAsync(id, ct);

        return Results.NoContent();
    }

    private static async Task<IResult> AgregarEvidenciaAsync(
        Guid id,
        [FromForm] string tipo,
        [FromForm] string titulo,
        [FromForm] string? contenidoJson,
        IFormFile? file,
        ClaimsPrincipal principal,
        AppDbContext db,
        IDocumentStorageResolver storageResolver,
        HttpContext http,
        CancellationToken ct)
    {
        var detalle = await RpaWorkerAuthorization.RequireClaimedStepAsync(db, principal.GetDespliegueId(), id, ct);
        if (detalle is null) return ProblemResults.NotFound(http, "Paso no encontrado.");

        var paso = await db.Set<EjecucionPaso>().AsNoTracking().FirstAsync(p => p.Id == id, ct);
        var caso = await db.Set<Caso>().AsNoTracking().FirstAsync(c => c.Id == paso.CasoId, ct);

        return await EvidenciaCreation.CrearAsync(
            paso.CasoId, id, tipo, titulo, contenidoJson, file, caso.FlujoId, uploadedByUserId: null, db, storageResolver, http, ct);
    }

    /// <summary>
    /// The robot tells people how it is going: how far along (a percentage), what it is doing, and where its screen can be
    /// watched. It only means something while the step is running, so a report for one that is over is a conflict, like any
    /// other late report. Only the live figure is kept: it does not go into the
    /// Caso's history.
    /// </summary>
    private static async Task<IResult> ReportarEnVivoAsync(
        Guid id,
        ReportarEnVivoRequest request,
        ReportarEnVivoRequestValidator validator,
        ClaimsPrincipal principal,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var detalle = await RpaWorkerAuthorization.RequireClaimedStepAsync(db, principal.GetDespliegueId(), id, ct);
        if (detalle is null) return ProblemResults.NotFound(http, "Paso no encontrado.");

        var paso = await db.Set<EjecucionPaso>().AsNoTracking().FirstAsync(p => p.Id == id, ct);
        if (paso.Estado != EjecucionPasoEstado.EnProgreso) return ProblemResults.Conflict(http, "Este paso ya no está en progreso.");

        var ahora = DateTimeOffset.UtcNow;

        if (request.Porcentaje is { } porcentaje) detalle.ProgresoPorcentaje = porcentaje;
        if (request.Mensaje is { } mensaje) detalle.ProgresoMensaje = string.IsNullOrWhiteSpace(mensaje) ? null : mensaje.Trim();
        if (request.VistaUrl is { } url) detalle.VistaEnDirectoUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        detalle.ProgresoAt = ahora;

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CambiarEstadoNegocioAsync(
        Guid id, CambiarEstadoNegocioRequest request, ClaimsPrincipal principal, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var detalle = await RpaWorkerAuthorization.RequireClaimedStepAsync(db, principal.GetDespliegueId(), id, ct);
        if (detalle is null) return ProblemResults.NotFound(http, "Paso no encontrado.");

        var paso = await db.Set<EjecucionPaso>().AsNoTracking().FirstAsync(p => p.Id == id, ct);
        var caso = await db.Set<Caso>().FirstAsync(c => c.Id == paso.CasoId, ct);

        var estado = await db.Set<FlujoEstadoDef>()
            .FirstOrDefaultAsync(e => e.FlujoId == caso.FlujoId && e.Codigo == request.Codigo && e.Activo, ct);
        if (estado is null) return ProblemResults.NotFound(http, "Estado de negocio no encontrado.");

        caso.EstadoNegocioActualId = estado.Id;
        caso.UpdatedAt = DateTimeOffset.UtcNow;
        db.Add(new CasoEvento
        {
            CasoId = caso.Id,
            EjecucionId = paso.EjecucionId,
            Accion = CasoEventoAccion.EstadoNegocioActualizado,
            DetalleJson = JsonSerializer.Serialize(new { estado.Codigo, estado.Display }),
            OccurredAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }
}
