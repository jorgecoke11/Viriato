using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
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
            await orchestrator.IniciarCasoAsync(caso.Id, pasoInicial?.Id, ct);
        }
        catch (InvalidOperationException ex)
        {
            return ProblemResults.Conflict(http, ex.Message);
        }

        return Results.Ok(new CasoCreadoDto(caso.Id, caso.Titulo));
    }

    private static async Task<IResult> ClaimSiguienteAsync(ClaimsPrincipal principal, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var despliegueId = principal.GetDespliegueId();
        var servicioId = principal.GetServicioId();
        var flujoId = principal.GetFlujoId();

        var despliegue = await db.Set<Despliegue>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == despliegueId, ct);
        if (despliegue is null) return ProblemResults.NotFound(http, "Despliegue no encontrado.");
        if (!despliegue.Encendido) return ProblemResults.Conflict(http, "El despliegue está apagado.");

        // Optimistic claim: pick the oldest unclaimed candidate, then a conditional UPDATE ... WHERE
        // DespliegueId IS NULL. If a concurrent poller won the race (0 rows affected), move to the
        // next candidate — cheap enough at this volume without raw-SQL row locking.
        for (var intento = 0; intento < 5; intento++)
        {
            var candidato = await (
                from detalle in db.Set<RpaEjecucionDetalle>().AsNoTracking()
                join paso in db.Set<EjecucionPaso>().AsNoTracking() on detalle.EjecucionPasoId equals paso.Id
                join pasoDef in db.Set<FlujoPasoDef>().AsNoTracking() on paso.FlujoPasoDefId equals pasoDef.Id
                join flujoVersion in db.Set<FlujoVersion>().AsNoTracking() on pasoDef.FlujoVersionId equals flujoVersion.Id
                where detalle.DespliegueId == null && paso.Estado == EjecucionPasoEstado.EnProgreso
                    && pasoDef.ServicioId == servicioId && flujoVersion.FlujoId == flujoId
                orderby detalle.Id
                select detalle.Id
            ).FirstOrDefaultAsync(ct);

            if (candidato == Guid.Empty)
            {
                return Results.NoContent();
            }

            var filasActualizadas = await db.Set<RpaEjecucionDetalle>()
                .Where(d => d.Id == candidato && d.DespliegueId == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(d => d.DespliegueId, despliegueId)
                    .SetProperty(d => d.ClaimedAt, DateTimeOffset.UtcNow), ct);

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
                where detalle.Id == candidato
                select new EjecucionAsignadaDto(
                    paso.Id, caso.Id, caso.Titulo, flujo.Nombre, detalle.AplicacionObjetivo, detalle.ParametrosEntrada, caso.DatosJson,
                    caso.TipoCaso == null ? null : caso.TipoCaso.Nombre)
            ).FirstAsync(ct);

            return Results.Ok(asignado);
        }

        return ProblemResults.Conflict(http, "No se pudo reservar un elemento de la cola; inténtalo de nuevo.");
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

        var paso = await db.Set<EjecucionPaso>().FirstAsync(p => p.Id == id, ct);
        if (paso.Estado != EjecucionPasoEstado.EnProgreso)
        {
            return ProblemResults.Conflict(http, "Este paso ya no está en progreso.");
        }

        detalle.ParametrosSalida = request.ParametrosSalida;
        paso.Estado = EjecucionPasoEstado.Completado;
        paso.FinishedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        if (paso.TrabajoId is { } trabajoId)
        {
            await tracker.CompleteAsync(trabajoId, summary: "Completado por el robot.", ct: ct);
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

        var paso = await db.Set<EjecucionPaso>().FirstAsync(p => p.Id == id, ct);
        if (paso.Estado != EjecucionPasoEstado.EnProgreso)
        {
            return ProblemResults.Conflict(http, "Este paso ya no está en progreso.");
        }

        detalle.ParametrosSalida = request.ParametrosSalida;
        paso.Estado = EjecucionPasoEstado.Completado;
        paso.FinishedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        if (paso.TrabajoId is { } trabajoId)
        {
            await tracker.CompleteAsync(trabajoId, summary: "Completado por el robot.", ct: ct);
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

        var paso = await db.Set<EjecucionPaso>().FirstAsync(p => p.Id == id, ct);
        if (paso.Estado != EjecucionPasoEstado.EnProgreso)
        {
            return ProblemResults.Conflict(http, "Este paso ya no está en progreso.");
        }

        paso.Estado = EjecucionPasoEstado.Fallido;
        paso.ErrorMensaje = request.Error;
        paso.FinishedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        if (paso.TrabajoId is { } trabajoId)
        {
            await tracker.FailAsync(trabajoId, request.Error, ct: ct);
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
