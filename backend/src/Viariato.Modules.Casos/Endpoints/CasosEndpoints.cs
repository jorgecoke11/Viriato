using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Contracts;
using Viariato.Modules.Casos.AccionesMasivas;
using Viariato.Modules.Casos.Despacho;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Orchestration;
using Viariato.Modules.Casos.Validation;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.RpaFleet.Domain;
using Viariato.Shared;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>
/// Everything here is hand-written — a Caso's lifecycle is real domain behavior (starting one
/// resolves and locks in a FlujoVersion; pausing/resuming/retrying delegate to the orchestrator),
/// not a plain field CRUD. Every handler that touches an existing Caso also enforces the
/// AsignacionFlujo boundary via <see cref="FlujoAccessAuthorization"/> — casos.read/manage/review
/// alone only proves the caller is allowed to use the Casos API at all, not which Flujos they can see.
/// </summary>
internal static class CasosEndpoints
{
    private static readonly CasoEstado[] EstadosActivos =
        [CasoEstado.Iniciado, CasoEstado.Pendiente, CasoEstado.EnProgreso, CasoEstado.Pausado, CasoEstado.EsperandoRevisionHumana];

    // The other half of the technical estados: the Caso is over, whatever the business estado it ended on says.
    private static readonly CasoEstado[] EstadosFinales = [CasoEstado.Completado, CasoEstado.Fallido, CasoEstado.Cancelado];

    public static void MapCasosCoreEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var read = endpoints.MapGroup("/api/v1/casos").RequireAuthorization(Permissions.CasosRead);
        read.MapGet("/", ListCasosAsync);
        read.MapGet("/resumen", GetResumenAsync);
        read.MapGet("/{id:guid}", GetCasoAsync);
        read.MapGet("/{id:guid}/eventos", ListEventosAsync);
        read.MapGet("/{id:guid}/evidencias", ListEvidenciasAsync);
        read.MapGet("/{id:guid}/ejecuciones", ListEjecucionesAsync);
        read.MapGet("/{id:guid}/ejecuciones/{ejecucionId:guid}", GetEjecucionAsync);
        read.MapGet("/{id:guid}/timeline", GetTimelineAsync);

        // Starting a case by hand picks the process, type and starting service freely, which is configuration; people create
        // cases through a creator (casos.crear, see CreadoresDeCasoUsoEndpoints).
        endpoints.MapGroup("/api/v1/casos").RequireAuthorization(Permissions.FlujosManage).MapPost("/", StartCasoAsync);

        var manage = endpoints.MapGroup("/api/v1/casos").RequireAuthorization(Permissions.CasosManage);
        manage.MapPatch("/{id:guid}/datos", UpdateDatosAsync);
        manage.MapPost("/{id:guid}/pausar", PausarAsync);
        manage.MapPost("/{id:guid}/reanudar", ReanudarAsync);
        manage.MapPost("/{id:guid}/pasos/{ejecucionPasoId:guid}/reprocesar", ReprocesarPasoAsync);

        var cancelar = endpoints.MapGroup("/api/v1/casos").RequireAuthorization(Permissions.CasosCancelar);
        cancelar.MapPost("/{id:guid}/cancelar", CancelarAsync);
        cancelar.MapPost("/{id:guid}/pasos/{ejecucionPasoId:guid}/cancelar", CancelarEjecucionAsync);

        endpoints.MapGroup("/api/v1/casos").RequireAuthorization(Permissions.CasosPrioridad)
            .MapPatch("/{id:guid}/pasos/{ejecucionPasoId:guid}/prioridad", CambiarPrioridadAsync);

        // The bulk-actions screen; each action then asks for its own permission (see EjecutarAccionMasivaAsync).
        endpoints.MapGroup("/api/v1/casos").RequireAuthorization(Permissions.CasosMasivas)
            .MapPost("/acciones/{accion}", EjecutarAccionMasivaAsync);
    }

    /// <summary>Null means access is granted; otherwise this is the response to return (NotFound,
    /// never Forbidden, so an unassigned user can't use the status code to confirm a Caso exists).</summary>
    private static async Task<IResult?> RequireAccesoAsync(AppDbContext db, HttpContext http, Guid flujoId, CancellationToken ct)
    {
        var tieneAcceso = await FlujoAccessAuthorization.TieneAccesoAlFlujoAsync(db, http.User.GetUserId(), flujoId, ct);
        return tieneAcceso ? null : ProblemResults.NotFound(http, "Caso no encontrado.");
    }

    /// <summary>
    /// Which Casos the dashboard counts: those still moving (an active estado) always, and of the finished ones only
    /// those that finished inside the window — nothing given -> since the start of today (UTC); <paramref name="finalizados"/>
    /// = "todos" -> no limit; <paramref name="desde"/>/<paramref name="hasta"/> -> that window over CompletedAt specifically,
    /// never over CreatedAt (a Caso created last month and finished today belongs to today). The summary and the
    /// drill-down lists both go through here, so what is listed is what was counted.
    /// </summary>
    private static IQueryable<Caso> ConVentanaDeFinalizados(
        IQueryable<Caso> query, string? finalizados, DateTimeOffset? desde, DateTimeOffset? hasta)
    {
        // EstadosActivos.Contains(c.Estado) is repeated inline (rather than pulled into a local
        // function) because EF Core can translate that exact shape into a SQL IN (...) clause — a
        // call to a local function inside the query wouldn't be translatable at all.
        if (desde is not null || hasta is not null)
        {
            return query.Where(c => EstadosActivos.Contains(c.Estado) || (c.CompletedAt != null
                && (desde == null || c.CompletedAt >= desde)
                && (hasta == null || c.CompletedAt <= hasta)));
        }

        if (finalizados == "todos")
        {
            return query.Where(c => EstadosActivos.Contains(c.Estado) || c.CompletedAt != null);
        }

        var hoyUtc = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        return query.Where(c => EstadosActivos.Contains(c.Estado) || (c.CompletedAt != null && c.CompletedAt >= hoyUtc));
    }

    private static async Task<IResult> ListCasosAsync(
        string? estado, string? tipoCasoId, string? estadoNegocioCodigo, bool? finalizado, Guid? flujoId, string? flujoIds, string? search,
        DateTimeOffset? desde, DateTimeOffset? hasta, bool? ventana, string? finalizados, DateTimeOffset? completadoDesde,
        DateTimeOffset? completadoHasta, bool? activos, int? page, int? pageSize, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var currentPage = page is null or < 1 ? 1 : page.Value;
        var currentPageSize = pageSize is null or < 1 or > 100 ? 20 : pageSize.Value;

        var flujosAsignados = await FlujoAccessAuthorization.FlujosAsignadosAsync(db, http.User.GetUserId(), ct);
        var query = db.Set<Caso>().AsNoTracking()
            .Include(c => c.EstadoNegocioActual).Include(c => c.TipoCaso)
            .Where(c => flujosAsignados.Contains(c.FlujoId));

        if (flujoId is not null) query = query.Where(c => c.FlujoId == flujoId);
        // Several processes at once, separated by commas. Whatever is not a process the caller is assigned to is simply not
        // in the list (the query is already limited to theirs), and text that is not an id is ignored.
        var procesos = (flujoIds ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => Guid.TryParse(id, out var parsed) ? (Guid?)parsed : null)
            .Where(id => id is not null).Select(id => id!.Value).ToArray();
        if (procesos.Length > 0) query = query.Where(c => procesos.Contains(c.FlujoId));
        // One estado or several separated by commas ("Pausado,EsperandoRevisionHumana"): the dashboard's "stopped" group is three.
        var estados = (estado ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => Enum.TryParse<CasoEstado>(e, true, out var parsed) ? (CasoEstado?)parsed : null)
            .Where(e => e is not null).Select(e => e!.Value).ToArray();
        if (estados.Length > 0) query = query.Where(c => estados.Contains(c.Estado));
        // "sin-tipo" is a sentinel from the dashboard's drill-down for the "Sin tipo" bucket (Casos
        // created before Tipo de caso existed, or without one chosen), since null can't travel as a
        // query string value.
        if (tipoCasoId == "sin-tipo")
        {
            query = query.Where(c => c.TipoCasoId == null);
        }
        else if (Guid.TryParse(tipoCasoId, out var parsedTipoCasoId))
        {
            query = query.Where(c => c.TipoCasoId == parsedTipoCasoId);
        }
        // Finished means the Caso itself is over (completed, failed or cancelled), not that its business estado is a final one:
        // the two are different questions and the dashboard's groups follow the first.
        if (finalizado is not null)
        {
            query = finalizado.Value
                ? query.Where(c => EstadosFinales.Contains(c.Estado))
                : query.Where(c => !EstadosFinales.Contains(c.Estado));
        }
        // "sin-estado" is a sentinel for the "Sin estado" bucket (Casos with no business estado
        // reported yet), since null can't travel as a query string value.
        if (estadoNegocioCodigo == "sin-estado")
        {
            query = query.Where(c => c.EstadoNegocioActualId == null);
        }
        else if (!string.IsNullOrWhiteSpace(estadoNegocioCodigo))
        {
            query = query.Where(c => c.EstadoNegocioActual != null && c.EstadoNegocioActual.Codigo == estadoNegocioCodigo);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.Titulo.Contains(term));
        }
        if (desde is not null) query = query.Where(c => c.CreatedAt >= desde);
        if (hasta is not null) query = query.Where(c => c.CreatedAt <= hasta);

        // Only the Casos that can still be acted on: waiting, running, paused or waiting for a person.
        if (activos == true) query = query.Where(c => EstadosActivos.Contains(c.Estado));

        // The same window the dashboard counts with, so a drill-down lists exactly the Casos that were counted.
        if (ventana == true) query = ConVentanaDeFinalizados(query, finalizados, completadoDesde, completadoHasta);

        query = query.OrderByDescending(c => c.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((currentPage - 1) * currentPageSize).Take(currentPageSize).ToListAsync(ct);

        // For the Casos a robot is running right now: how far along it says it is, and whether its screen can be watched.
        var idsDeLaPagina = items.Select(c => c.Id).ToList();
        var enMarcha = (await db.Set<EjecucionPaso>().AsNoTracking()
                .Where(p => idsDeLaPagina.Contains(p.CasoId) && p.Estado == EjecucionPasoEstado.EnProgreso)
                .Join(db.Set<RpaEjecucionDetalle>().Where(d => d.DespliegueId != null), p => p.Id, d => d.EjecucionPasoId,
                    (p, d) => new { p.CasoId, d.ProgresoPorcentaje, d.VistaEnDirectoUrl })
                .ToListAsync(ct))
            .GroupBy(x => x.CasoId)
            .ToDictionary(g => g.Key, g => g.First());

        return Results.Ok(new PagedResult<CasoListItemDto>(
            items.Select(c => enMarcha.TryGetValue(c.Id, out var vivo) ? c.ToListItemDto(vivo.ProgresoPorcentaje, vivo.VistaEnDirectoUrl is not null) : c.ToListItemDto()).ToList(),
            currentPage, currentPageSize, total));
    }

    /// <summary>
    /// One card per Flujo the caller is assigned to (or just the one named by <paramref name="flujoId"/>,
    /// for a single card's own override), broken down by Tipo de caso. Casos in an active estado
    /// always count, regardless of any date filter — "still moving" isn't a question date narrows
    /// down. Only finalized Casos (an active final estado) are filtered by when they completed:
    /// nothing given -> today; <paramref name="finalizados"/>="todos" -> no limit; desde/hasta -> that
    /// window over CompletedAt specifically, never over CreatedAt (a finalized Caso from last month
    /// created last month would otherwise wrongly survive a "today" style range check on CreatedAt).
    /// </summary>
    private static async Task<IResult> GetResumenAsync(
        string? finalizados, DateTimeOffset? desde, DateTimeOffset? hasta, Guid? flujoId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var flujosAsignados = await FlujoAccessAuthorization.FlujosAsignadosAsync(db, http.User.GetUserId(), ct);
        if (flujosAsignados.Count == 0) return Results.Ok(Array.Empty<FlujoResumenDto>());

        var query = db.Set<Caso>().AsNoTracking().Where(c => flujosAsignados.Contains(c.FlujoId));
        if (flujoId is not null) query = query.Where(c => c.FlujoId == flujoId);

        query = ConVentanaDeFinalizados(query, finalizados, desde, hasta);

        var casosCrudos = await query
            .Select(c => new
            {
                c.FlujoId,
                c.TipoCasoId,
                TipoCasoNombre = c.TipoCaso == null ? null : c.TipoCaso.Nombre,
                TipoCasoOrden = c.TipoCaso == null ? (int?)null : c.TipoCaso.Orden,
                EstadoCodigo = c.EstadoNegocioActual == null ? null : c.EstadoNegocioActual.Codigo,
                EstadoDisplay = c.EstadoNegocioActual == null ? null : c.EstadoNegocioActual.Display,
                EstadoOrden = c.EstadoNegocioActual == null ? (int?)null : c.EstadoNegocioActual.Orden,
                c.Estado,
                EstadoActivo = c.EstadoNegocioActual != null && c.EstadoNegocioActual.Activo,
                Finalizado = EstadosFinales.Contains(c.Estado),
                EstadoEsFinal = c.EstadoNegocioActual != null && c.EstadoNegocioActual.EsFinal && c.EstadoNegocioActual.Activo,
            })
            .ToListAsync(ct);

        // A retired estado (Activo=false) shouldn't be reported as its own group anymore — Casos still
        // pointing at one fold into "Sin estado" for this breakdown, even though their FK is untouched.
        var casos = casosCrudos.Select(c => c.EstadoActivo
            ? c
            : c with { EstadoCodigo = null, EstadoDisplay = null, EstadoOrden = null }).ToList();

        var flujos = await db.Set<Flujo>().AsNoTracking()
            .Where(f => flujosAsignados.Contains(f.Id))
            .OrderBy(f => f.Nombre)
            .ToListAsync(ct);

        // How many of each process's parameters the person may change from the card: the card only offers the action if there are any.
        var editablesPorFlujo = await db.Set<FlujoParametro>().AsNoTracking()
            .Where(p => p.EditablePorUsuario && flujosAsignados.Contains(p.FlujoId))
            .GroupBy(p => p.FlujoId)
            .Select(g => new { FlujoId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.FlujoId, x => x.Cantidad, ct);

        // Grouped by Tipo de caso, not by estado — a type with no Caso in it doesn't appear at all.
        // "Sin tipo" (TipoCasoId null) shows up only when a Caso in range hasn't had one chosen. Within
        // each type, the same Casos are grouped again by their current business estado for the
        // accordion's expanded detail — an estado with nothing in it doesn't appear there either.
        var resumen = flujos.Select(f =>
        {
            var casosDelFlujo = casos.Where(c => c.FlujoId == f.Id).ToList();
            var porTipo = casosDelFlujo
                .GroupBy(c => c.TipoCasoId)
                .Select(g =>
                {
                    var porEstado = g
                        .GroupBy(c => c.EstadoCodigo)
                        .Select(eg => new EstadoConteoDto(
                            eg.Key, eg.First().EstadoDisplay ?? "Sin estado", eg.First().EstadoOrden ?? int.MaxValue, eg.Count(), eg.First().EstadoEsFinal))
                        .OrderBy(e => e.Orden)
                        .ToList();

                    return new TipoCasoConteoDto(
                        g.Key,
                        g.First().TipoCasoNombre ?? "Sin tipo",
                        g.First().TipoCasoOrden ?? int.MaxValue,
                        g.Count(c => !c.Finalizado),
                        g.Count(c => c.Finalizado),
                        porEstado,
                        g.Count(c => c.Estado == CasoEstado.EnProgreso),
                        g.Count(c => c.Estado == CasoEstado.Pendiente),
                        g.Count(c => !c.Finalizado && c.Estado != CasoEstado.EnProgreso && c.Estado != CasoEstado.Pendiente));
                })
                .OrderBy(t => t.Orden)
                .ToList();

            return new FlujoResumenDto(f.Id, f.Nombre, casosDelFlujo.Count, porTipo, editablesPorFlujo.GetValueOrDefault(f.Id));
        }).ToList();

        return Results.Ok(resumen);
    }

    private static async Task<IResult> GetCasoAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var caso = await db.Set<Caso>().AsNoTracking()
            .Include(c => c.EstadoNegocioActual).Include(c => c.TipoCaso)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (caso is null) return ProblemResults.NotFound(http, "Caso no encontrado.");
        if (await RequireAccesoAsync(db, http, caso.FlujoId, ct) is { } denied) return denied;

        EjecucionDto? ejecucionDto = null;
        if (caso.EjecucionActualId is not null)
        {
            var ejecucion = await db.Set<Ejecucion>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == caso.EjecucionActualId, ct);
            if (ejecucion is not null)
            {
                var pasos = await db.Set<EjecucionPaso>().AsNoTracking()
                    .Where(p => p.EjecucionId == ejecucion.Id)
                    .OrderBy(p => p.CreatedAt)
                    .ToListAsync(ct);
                ejecucionDto = ejecucion.ToDto(await ConPrioridadAsync(db, pasos, ct));
            }
        }

        var eventos = await db.Set<CasoEvento>().AsNoTracking()
            .Where(e => e.CasoId == id)
            .OrderByDescending(e => e.OccurredAt)
            .Take(20)
            .Select(e => e.ToDto())
            .ToListAsync(ct);

        return Results.Ok(caso.ToDetailDto(ejecucionDto, eventos));
    }

    private static async Task<IResult> ListEventosAsync(
        Guid id, int? page, int? pageSize, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var caso = await db.Set<Caso>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (caso is null) return ProblemResults.NotFound(http, "Caso no encontrado.");
        if (await RequireAccesoAsync(db, http, caso.FlujoId, ct) is { } denied) return denied;

        var currentPage = page is null or < 1 ? 1 : page.Value;
        var currentPageSize = pageSize is null or < 1 or > 100 ? 20 : pageSize.Value;

        var query = db.Set<CasoEvento>().AsNoTracking().Where(e => e.CasoId == id).OrderByDescending(e => e.OccurredAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((currentPage - 1) * currentPageSize).Take(currentPageSize).Select(e => e.ToDto()).ToListAsync(ct);

        return Results.Ok(new PagedResult<CasoEventoDto>(items, currentPage, currentPageSize, total));
    }

    private static async Task<IResult> ListEvidenciasAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var caso = await db.Set<Caso>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (caso is null) return ProblemResults.NotFound(http, "Caso no encontrado.");
        if (await RequireAccesoAsync(db, http, caso.FlujoId, ct) is { } denied) return denied;

        var evidencias = await db.Set<Evidencia>().AsNoTracking()
            .Where(e => e.CasoId == id)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => e.ToDto())
            .ToListAsync(ct);

        return Results.Ok(evidencias);
    }

    /// <summary>
    /// One Rpa transitioning into a second Rpa is two Ejecuciones, not one — this lists all of them
    /// for the Caso; drilling into a specific one (GetEjecucionAsync) is where its own step-by-step
    /// progress history lives.
    /// </summary>
    private static async Task<IResult> ListEjecucionesAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var caso = await db.Set<Caso>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (caso is null) return ProblemResults.NotFound(http, "Caso no encontrado.");
        if (await RequireAccesoAsync(db, http, caso.FlujoId, ct) is { } denied) return denied;

        var ejecuciones = await db.Set<Ejecucion>().AsNoTracking()
            .Where(e => e.CasoId == id)
            .OrderBy(e => e.StartedAt)
            .Select(e => new EjecucionResumenDto(
                e.Id, e.CasoId, e.Estado.ToString(),
                db.Set<EjecucionPaso>().Count(p => p.EjecucionId == e.Id),
                e.StartedAt, e.FinishedAt))
            .ToListAsync(ct);

        return Results.Ok(ejecuciones);
    }

    private static async Task<IResult> GetEjecucionAsync(Guid id, Guid ejecucionId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var caso = await db.Set<Caso>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (caso is null) return ProblemResults.NotFound(http, "Caso no encontrado.");
        if (await RequireAccesoAsync(db, http, caso.FlujoId, ct) is { } denied) return denied;

        var ejecucion = await db.Set<Ejecucion>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == ejecucionId && e.CasoId == id, ct);
        if (ejecucion is null) return ProblemResults.NotFound(http, "Ejecución no encontrada.");

        var pasos = await db.Set<EjecucionPaso>().AsNoTracking()
            .Where(p => p.EjecucionId == ejecucion.Id)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

        return Results.Ok(ejecucion.ToDto(await ConPrioridadAsync(db, pasos, ct)));
    }

    /// <summary>
    /// The Caso-level view: business-status changes, document uploads, and evidence, interleaved
    /// chronologically. Deliberately excludes the granular technical events (PasoIniciado,
    /// PasoCompletado, etc.) — those belong to a specific Ejecucion's own history, not this feed.
    /// </summary>
    private static async Task<IResult> GetTimelineAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var caso = await db.Set<Caso>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (caso is null) return ProblemResults.NotFound(http, "Caso no encontrado.");
        if (await RequireAccesoAsync(db, http, caso.FlujoId, ct) is { } denied) return denied;

        var eventosEstado = await db.Set<CasoEvento>().AsNoTracking()
            .Where(e => e.CasoId == id && e.Accion == CasoEventoAccion.EstadoNegocioActualizado)
            .Select(e => new { e.Id, e.OccurredAt, e.DetalleJson })
            .ToListAsync(ct);

        // DetalleJson holds {"Codigo":"...","Display":"..."} (see EjecucionOrchestrator) — extract
        // Display for the title and Codigo for the frontend's icon/label, never show the raw JSON.
        var cambiosEstado = eventosEstado.Select(e =>
        {
            var titulo = "Estado actualizado";
            string? codigo = null;
            if (e.DetalleJson is not null)
            {
                try
                {
                    using var json = JsonDocument.Parse(e.DetalleJson);
                    titulo = json.RootElement.TryGetProperty("Display", out var d) ? d.GetString() ?? titulo : titulo;
                    codigo = json.RootElement.TryGetProperty("Codigo", out var c) ? c.GetString() : null;
                }
                catch (JsonException) { }
            }
            return new CasoTimelineItemDto(e.Id, CasoTimelineItemTipo.EstadoCambiado, e.OccurredAt, titulo, codigo, null, null, null);
        }).ToList();

        // Evidencia files already appear below as Evidencia items — listing their backing Documento row
        // too would show every screenshot twice.
        var documentos = await db.Set<Documento>().AsNoTracking()
            .Where(d => d.CasoId == id && !db.Set<Evidencia>().Any(e => e.DocumentoId == d.Id))
            .Select(d => new CasoTimelineItemDto(
                d.Id, CasoTimelineItemTipo.Documento, d.CreatedAt, d.Nombre, null, d.Id, null, null,
                db.Set<EjecucionPaso>().Where(p => p.Id == d.EjecucionPasoId).Select(p => p.FlujoPasoDef!.Nombre).FirstOrDefault(),
                d.Nombre, d.ContentType, d.TamanoBytes))
            .ToListAsync(ct);

        var evidencias = await db.Set<Evidencia>().AsNoTracking()
            .Where(e => e.CasoId == id)
            .Select(e => new CasoTimelineItemDto(
                e.Id, CasoTimelineItemTipo.Evidencia, e.CreatedAt, e.Titulo, null, e.DocumentoId, e.Tipo.ToString(), e.ContenidoJson,
                db.Set<EjecucionPaso>().Where(p => p.Id == e.EjecucionPasoId).Select(p => p.FlujoPasoDef!.Nombre).FirstOrDefault(),
                db.Set<Documento>().Where(d => d.Id == e.DocumentoId).Select(d => d.Nombre).FirstOrDefault(),
                db.Set<Documento>().Where(d => d.Id == e.DocumentoId).Select(d => d.ContentType).FirstOrDefault(),
                db.Set<Documento>().Where(d => d.Id == e.DocumentoId).Select(d => (long?)d.TamanoBytes).FirstOrDefault()))
            .ToListAsync(ct);

        var timeline = cambiosEstado.Concat(documentos).Concat(evidencias)
            .OrderByDescending(item => item.OccurredAt)
            .ToList();

        return Results.Ok(timeline);
    }

    private static async Task<IResult> StartCasoAsync(
        StartCasoRequest request,
        StartCasoRequestValidator validator,
        AppDbContext db,
        IEjecucionOrchestrator orchestrator,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        return await IniciarCasoAsync(request, db, orchestrator, http, ct);
    }

    /// <summary>Starts a Caso from an already validated request. Shared with the creators, which are only a pre-filled way of
    /// asking for the same thing — same checks, same boundary, same result.</summary>
    internal static async Task<IResult> IniciarCasoAsync(
        StartCasoRequest request, AppDbContext db, IEjecucionOrchestrator orchestrator, HttpContext http, CancellationToken ct)
    {
        FlujoVersion? version;
        if (request.FlujoVersionId is not null)
        {
            version = await db.Set<FlujoVersion>().FirstOrDefaultAsync(v => v.Id == request.FlujoVersionId, ct);
        }
        else
        {
            var flujo = await db.Set<Flujo>().FirstOrDefaultAsync(f => f.Id == request.FlujoId, ct);
            if (flujo is null) return ProblemResults.NotFound(http, "Flujo no encontrado.");
            if (flujo.VersionActivaId is null) return ProblemResults.Conflict(http, "El flujo no tiene una versión activa.");
            version = await db.Set<FlujoVersion>().FirstOrDefaultAsync(v => v.Id == flujo.VersionActivaId, ct);
        }

        if (version is null) return ProblemResults.NotFound(http, "FlujoVersion no encontrada.");

        // Starting a Caso in a Flujo you're not assigned to is the same "you can't even see this
        // exists" boundary as reading one — checked before the FlujoVersionEstado check below so an
        // unassigned caller can't use the ordering of error responses to learn anything about it.
        var tieneAcceso = await FlujoAccessAuthorization.TieneAccesoAlFlujoAsync(db, http.User.GetUserId(), version.FlujoId, ct);
        if (!tieneAcceso) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        if (version.Estado != FlujoVersionEstado.Publicada)
        {
            return ProblemResults.Conflict(http, "Solo se puede iniciar un Caso con una FlujoVersion publicada.");
        }

        FlujoEstadoDef? estadoNegocioInicial = null;
        if (request.EstadoNegocioInicialId is not null)
        {
            estadoNegocioInicial = await db.Set<FlujoEstadoDef>()
                .FirstOrDefaultAsync(e => e.Id == request.EstadoNegocioInicialId && e.FlujoId == version.FlujoId && e.Activo, ct);
            if (estadoNegocioInicial is null)
            {
                return ProblemResults.Conflict(http, "El estado de negocio indicado no pertenece a este flujo.");
            }
        }

        if (request.TipoCasoId is not null)
        {
            var tipoPerteneceAlFlujo = await db.Set<FlujoTipoCasoDef>()
                .AnyAsync(t => t.Id == request.TipoCasoId && t.FlujoId == version.FlujoId, ct);
            if (!tipoPerteneceAlFlujo)
            {
                return ProblemResults.Conflict(http, "El tipo de caso indicado no pertenece a este flujo.");
            }
        }

        if (await DatosPorTipo.RechazarSiNoCumpleAsync(db, request.TipoCasoId, request.DatosJson, ct) is { } datosRechazados)
        {
            return datosRechazados;
        }

        if (request.PasoInicialId is not null)
        {
            var pasoInicialPerteneceALaVersion = await db.Set<FlujoPasoDef>()
                .AnyAsync(p => p.Id == request.PasoInicialId && p.FlujoVersionId == version.Id, ct);
            if (!pasoInicialPerteneceALaVersion)
            {
                return ProblemResults.Conflict(http, "El paso inicial indicado no pertenece a esta versión del flujo.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var caso = new Caso
        {
            FlujoId = version.FlujoId,
            FlujoVersionId = version.Id,
            Titulo = request.Titulo,
            DatosJson = request.DatosJson,
            TipoCasoId = request.TipoCasoId,
            EstadoNegocioActualId = estadoNegocioInicial?.Id,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUserId = http.User.GetUserId(),
        };

        db.Add(caso);
        db.Add(new CasoEvento { CasoId = caso.Id, Accion = CasoEventoAccion.Creado, OccurredAt = now });
        if (estadoNegocioInicial is not null)
        {
            db.Add(new CasoEvento
            {
                CasoId = caso.Id,
                Accion = CasoEventoAccion.EstadoNegocioActualizado,
                DetalleJson = JsonSerializer.Serialize(new { estadoNegocioInicial.Codigo, estadoNegocioInicial.Display }),
                OccurredAt = now,
            });
        }
        await db.SaveChangesAsync(ct);

        try
        {
            // Defaults to the flow's first step when PasoInicialId is null — the caller can instead
            // pick which servicio-backed step to launch first (e.g. skipping a preamble already done
            // elsewhere); everything before it is recorded as Omitido (see IniciarCasoAsync).
            await orchestrator.IniciarCasoAsync(caso.Id, request.PasoInicialId, ct);
        }
        catch (InvalidOperationException ex)
        {
            return ProblemResults.Conflict(http, ex.Message);
        }

        var actualizado = await db.Set<Caso>().AsNoTracking()
            .Include(c => c.EstadoNegocioActual).Include(c => c.TipoCaso)
            .FirstAsync(c => c.Id == caso.Id, ct);
        return Results.Ok(actualizado.ToListItemDto());
    }

    /// <summary>
    /// Changes where an execution stands in the queue of the service it is waiting for. It takes effect at once: the
    /// queue reads the priority every time a robot asks for work, so there is nothing to reorder. Only an execution
    /// that is still waiting can change — once a robot has taken it, or it has finished, its place in the queue means
    /// nothing, and saying so is better than silently accepting a number that does nothing.
    /// </summary>
    private static async Task<IResult> CambiarPrioridadAsync(
        Guid id,
        Guid ejecucionPasoId,
        CambiarPrioridadRequest request,
        CambiarPrioridadRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var caso = await db.Set<Caso>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (caso is null) return ProblemResults.NotFound(http, "Caso no encontrado.");
        if (await RequireAccesoAsync(db, http, caso.FlujoId, ct) is { } denied) return denied;

        var paso = await db.Set<EjecucionPaso>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == ejecucionPasoId && p.CasoId == id, ct);
        if (paso is null) return ProblemResults.NotFound(http, "Ejecución no encontrada.");

        var detalle = await db.Set<RpaEjecucionDetalle>().FirstOrDefaultAsync(d => d.EjecucionPasoId == ejecucionPasoId, ct);
        if (detalle is null) return ProblemResults.Conflict(http, "Solo las ejecuciones de un robot tienen prioridad.");
        if (paso.Estado != EjecucionPasoEstado.EnProgreso || detalle.DespliegueId is not null)
        {
            return ProblemResults.Conflict(http, "Solo se puede cambiar la prioridad de una ejecución que sigue esperando a un robot.");
        }

        if (detalle.Prioridad != request.Prioridad)
        {
            var anterior = detalle.Prioridad;
            detalle.Prioridad = request.Prioridad;
            db.Add(new CasoEvento
            {
                CasoId = id,
                ActorUserId = http.User.GetUserId(),
                EjecucionId = paso.EjecucionId,
                Accion = CasoEventoAccion.PrioridadCambiada,
                DetalleJson = JsonSerializer.Serialize(new { ejecucionPasoId, anterior, nueva = request.Prioridad }),
                OccurredAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(paso.ToDto(detalle));
    }

    /// <summary>
    /// Applies one of the bulk actions (see <see cref="IAccionMasivaSobreCaso"/>) to a selection of Casos. The action is named in
    /// the URL; what is common to all of them — the selection checks, the report of what was done and what was left alone — is
    /// done once, by <see cref="EjecutorDeAccionesMasivas"/>, so this endpoint never changes when an action is added.
    /// </summary>
    private static async Task<IResult> EjecutarAccionMasivaAsync(
        string accion,
        AccionMasivaRequest request,
        AccionMasivaRequestValidator validator,
        IEnumerable<IAccionMasivaSobreCaso> acciones,
        EjecutorDeAccionesMasivas ejecutor,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var implementacion = acciones.FirstOrDefault(a => string.Equals(a.Id, accion, StringComparison.OrdinalIgnoreCase));
        if (implementacion is null) return ProblemResults.NotFound(http, $"La acción «{accion}» no existe.");

        // The same rule the permission policies apply (a "perm" claim), without needing a policy per action.
        if (!http.User.HasClaim("perm", implementacion.Permiso))
        {
            return ProblemResults.Forbidden(http, "No tienes permiso para esta acción.");
        }

        if (implementacion.ValidarParametros(request.Parametros) is { } problema)
        {
            return ProblemResults.ValidationProblem(new FluentValidation.Results.ValidationResult(
                [new FluentValidation.Results.ValidationFailure(nameof(request.Parametros), problema)]));
        }

        return Results.Ok(await ejecutor.EjecutarAsync(implementacion, request.Ids, request.Parametros, http.User.GetUserId(), ct));
    }

    /// <summary>
    /// Cancels an execution — an RPA step that waits in the queue or that a robot is running — and with it its Caso. It does
    /// not stop a robot that is already working on it (the platform cannot reach into the machine): it ignores whatever the
    /// robot reports afterwards, which is turned down with a 409.
    /// </summary>
    private static async Task<IResult> CancelarEjecucionAsync(
        Guid id, Guid ejecucionPasoId, ControlDePasos control, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var caso = await db.Set<Caso>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (caso is null) return ProblemResults.NotFound(http, "Caso no encontrado.");
        if (await RequireAccesoAsync(db, http, caso.FlujoId, ct) is { } denied) return denied;

        var paso = await db.Set<EjecucionPaso>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == ejecucionPasoId && p.CasoId == id, ct);
        if (paso is null) return ProblemResults.NotFound(http, "Ejecución no encontrada.");
        if (!await db.Set<RpaEjecucionDetalle>().AnyAsync(d => d.EjecucionPasoId == ejecucionPasoId, ct))
        {
            return ProblemResults.Conflict(http, "Solo se pueden cancelar las ejecuciones de un robot.");
        }

        if (!await control.CancelarEjecucionAsync(ejecucionPasoId, id, ct))
        {
            return ProblemResults.Conflict(http, "Solo se puede cancelar una ejecución que está esperando a un robot o en marcha.");
        }

        return Results.NoContent();
    }

    /// <summary>The robot-side detail of each RPA step among <paramref name="pasos"/> for the DTOs: its priority, the service whose
    /// robots run it and, once a robot has taken it, the machine that robot is on.</summary>
    private static async Task<List<EjecucionPasoDto>> ConPrioridadAsync(AppDbContext db, IReadOnlyList<EjecucionPaso> pasos, CancellationToken ct)
    {
        var ids = pasos.Select(p => p.Id).ToList();
        var detalles = await db.Set<RpaEjecucionDetalle>().AsNoTracking()
            .Where(d => ids.Contains(d.EjecucionPasoId)).ToDictionaryAsync(d => d.EjecucionPasoId, ct);
        if (detalles.Count == 0) return pasos.Select(p => p.ToDto()).ToList();

        var definiciones = pasos.Where(p => detalles.ContainsKey(p.Id)).Select(p => p.FlujoPasoDefId).Distinct().ToList();
        var serviciosDeLaDefinicion = await (
            from definicion in db.Set<FlujoPasoDef>().AsNoTracking()
            join servicio in db.Set<Servicio>().AsNoTracking() on definicion.ServicioId equals servicio.Id
            where definiciones.Contains(definicion.Id)
            select new { definicion.Id, servicio.Nombre }).ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);

        var despliegueIds = detalles.Values.Where(d => d.DespliegueId != null).Select(d => d.DespliegueId!.Value).Distinct().ToList();
        var equiposPorDespliegue = despliegueIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await (
                from despliegue in db.Set<Despliegue>().AsNoTracking()
                join equipo in db.Set<Equipo>().AsNoTracking() on despliegue.EquipoId equals equipo.Id
                where despliegueIds.Contains(despliegue.Id)
                select new { despliegue.Id, equipo.Nombre }).ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);

        return pasos.Select(p =>
        {
            var detalle = detalles.GetValueOrDefault(p.Id);
            return p.ToDto(
                detalle,
                serviciosDeLaDefinicion.GetValueOrDefault(p.FlujoPasoDefId),
                detalle?.DespliegueId is { } despliegueId ? equiposPorDespliegue.GetValueOrDefault(despliegueId) : null);
        }).ToList();
    }

    private static async Task<IResult> UpdateDatosAsync(
        Guid id,
        UpdateCasoDatosRequest request,
        UpdateCasoDatosRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var caso = await db.Set<Caso>().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (caso is null) return ProblemResults.NotFound(http, "Caso no encontrado.");
        if (await RequireAccesoAsync(db, http, caso.FlujoId, ct) is { } denied) return denied;
        if (await DatosPorTipo.RechazarSiNoCumpleAsync(db, caso.TipoCasoId, request.DatosJson, ct) is { } datosRechazados) return datosRechazados;

        caso.DatosJson = request.DatosJson;
        caso.UpdatedAt = DateTimeOffset.UtcNow;
        db.Add(new CasoEvento
        {
            CasoId = id,
            EjecucionId = caso.EjecucionActualId,
            Accion = CasoEventoAccion.DatosActualizados,
            OccurredAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        await db.Entry(caso).Reference(c => c.EstadoNegocioActual).LoadAsync(ct);
        await db.Entry(caso).Reference(c => c.TipoCaso).LoadAsync(ct);

        return Results.Ok(caso.ToListItemDto());
    }

    private static Task<IResult> PausarAsync(Guid id, IEjecucionOrchestrator orchestrator, AppDbContext db, HttpContext http, CancellationToken ct) =>
        ConAccesoAsync(db, http, id, ct, () => EjecutarComandoAsync(orchestrator.PausarAsync(id, ct), http, () => Results.NoContent()));

    private static Task<IResult> ReanudarAsync(Guid id, IEjecucionOrchestrator orchestrator, AppDbContext db, HttpContext http, CancellationToken ct) =>
        ConAccesoAsync(db, http, id, ct, () => EjecutarComandoAsync(orchestrator.ReanudarAsync(id, ct), http, () => Results.NoContent()));

    private static Task<IResult> CancelarAsync(Guid id, IEjecucionOrchestrator orchestrator, AppDbContext db, HttpContext http, CancellationToken ct) =>
        ConAccesoAsync(db, http, id, ct, () => EjecutarComandoAsync(orchestrator.CancelarAsync(id, ct), http, () => Results.NoContent()));

    private static Task<IResult> ReprocesarPasoAsync(
        Guid id, Guid ejecucionPasoId, IEjecucionOrchestrator orchestrator, AppDbContext db, HttpContext http, CancellationToken ct) =>
        ConAccesoAsync(db, http, id, ct, () => EjecutarComandoAsync(orchestrator.ReprocesarPasoAsync(ejecucionPasoId, ct), http, () => Results.NoContent()));

    /// <summary>Shared "load the Caso, enforce assignment, then run the command" wrapper for the
    /// orchestrator-delegating endpoints, which otherwise only take the Caso's id.</summary>
    private static async Task<IResult> ConAccesoAsync(AppDbContext db, HttpContext http, Guid casoId, CancellationToken ct, Func<Task<IResult>> accion)
    {
        var caso = await db.Set<Caso>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == casoId, ct);
        if (caso is null) return ProblemResults.NotFound(http, "Caso no encontrado.");
        if (await RequireAccesoAsync(db, http, caso.FlujoId, ct) is { } denied) return denied;

        return await accion();
    }

    /// <summary>The orchestrator throws InvalidOperationException for domain-rule violations (e.g.
    /// "solo se puede reanudar una ejecución pausada") — translated here into a 409 instead of a 500.</summary>
    private static async Task<IResult> EjecutarComandoAsync(Task comando, HttpContext http, Func<IResult> onSuccess)
    {
        try
        {
            await comando;
            return onSuccess();
        }
        catch (InvalidOperationException ex)
        {
            return ProblemResults.Conflict(http, ex.Message);
        }
    }
}
