using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Familias;
using Viariato.Modules.Flujos.Domain;
using Viariato.Shared;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

public sealed record TipoDeFamiliaDto(
    Guid Id, Guid FlujoId, string Nombre, string? Descripcion, bool Activo, Guid? CreadorAlTerminarId, string? CreadorAlTerminarNombre,
    string? CreadorAlTerminarProceso, string Condicion, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record GuardarTipoDeFamiliaRequest(string Nombre, string? Descripcion, bool Activo, Guid? CreadorAlTerminarId, string? Condicion);

/// <summary>A family in a list: its progress so far (how many of its members have ended) and, once ended, how they ended.</summary>
public sealed record FamiliaDto(
    Guid Id, Guid FlujoId, string FlujoNombre, Guid TipoDeFamiliaId, string Tipo, string Nombre, string Estado, int? ParticipantesEsperados,
    int Miembros, int Terminados, int? Completados, int? Fallidos, int? Cancelados, Guid? CasoDeCierreId, string? CasoDeCierreTitulo,
    string? NotaDeCierre, DateTimeOffset CreatedAt, DateTimeOffset? CerradaAt, DateTimeOffset? TerminadaAt);

public sealed record FamiliaEventoDto(Guid Id, string Accion, string? DetalleJson, DateTimeOffset OccurredAt);

public sealed record CrearFamiliaRequest(Guid FlujoId, Guid TipoDeFamiliaId, string Nombre, int? ParticipantesEsperados, IReadOnlyList<Guid>? CasoIds);

public sealed record AnadirCasosAFamiliaRequest(IReadOnlyList<Guid> CasoIds);

/// <summary>
/// Families for people. Their types are the process's configuration (who configures the process); families themselves are seen by whoever
/// sees the process's cases, and made, filled and closed by whoever can create its cases. Ending one is never asked for: it ends when its
/// cases do (<see cref="TerminadorDeFamilias"/>).
/// </summary>
internal static class FamiliasEndpoints
{
    private static readonly CasoEstado[] EstadosFinales = [CasoEstado.Completado, CasoEstado.Fallido, CasoEstado.Cancelado];

    public static void MapFamiliasEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var tiposLectura = endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/tipos-de-familia").RequireAuthorization(Permissions.CasosRead);
        tiposLectura.MapGet("/", ListarTiposAsync);

        var tipos = endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/tipos-de-familia").RequireAuthorization(Permissions.FlujosManage);
        tipos.MapPost("/", CrearTipoAsync);
        tipos.MapPut("/{id:guid}", ActualizarTipoAsync);
        tipos.MapDelete("/{id:guid}", BorrarTipoAsync);

        var lectura = endpoints.MapGroup("/api/v1/familias").RequireAuthorization(Permissions.CasosRead);
        lectura.MapGet("/", ListarAsync);
        lectura.MapGet("/{id:guid}", ObtenerAsync);
        lectura.MapGet("/{id:guid}/eventos", EventosAsync);
        endpoints.MapGroup("/api/v1/casos").RequireAuthorization(Permissions.CasosRead).MapGet("/{id:guid}/familias", DeUnCasoAsync);

        var gestion = endpoints.MapGroup("/api/v1/familias").RequireAuthorization(Permissions.CasosCrear);
        gestion.MapPost("/", CrearAsync);
        gestion.MapPost("/{id:guid}/cerrar", CerrarAsync);
        gestion.MapPost("/{id:guid}/casos", AnadirCasosAsync);
    }

    // ── Types ────────────────────────────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> ListarTiposAsync(Guid flujoId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (!await FlujoAccessAuthorization.TieneAccesoAlFlujoAsync(db, http.User.GetUserId(), flujoId, ct)) return ProblemResults.NotFound(http, "Proceso no encontrado.");

        var tipos = await db.Set<TipoDeFamilia>().AsNoTracking().Where(t => t.FlujoId == flujoId).OrderBy(t => t.Nombre).ToListAsync(ct);
        return Results.Ok(await TiposADtoAsync(db, tipos, ct));
    }

    private static async Task<IResult> CrearTipoAsync(Guid flujoId, GuardarTipoDeFamiliaRequest request, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (!await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct)) return ProblemResults.NotFound(http, "Proceso no encontrado.");
        if (await ComprobarTipoAsync(flujoId, null, request, db, ct) is { } incorrecto) return incorrecto;

        var ahora = DateTimeOffset.UtcNow;
        var tipo = new TipoDeFamilia { FlujoId = flujoId, Nombre = string.Empty, CreatedAt = ahora };
        AplicarTipo(tipo, request, ahora);
        db.Add(tipo);
        await db.SaveChangesAsync(ct);
        return Results.Ok((await TiposADtoAsync(db, [tipo], ct))[0]);
    }

    private static async Task<IResult> ActualizarTipoAsync(
        Guid flujoId, Guid id, GuardarTipoDeFamiliaRequest request, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var tipo = await db.Set<TipoDeFamilia>().FirstOrDefaultAsync(t => t.Id == id && t.FlujoId == flujoId, ct);
        if (tipo is null) return ProblemResults.NotFound(http, "Tipo de familia no encontrado.");
        if (await ComprobarTipoAsync(flujoId, id, request, db, ct) is { } incorrecto) return incorrecto;

        AplicarTipo(tipo, request, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return Results.Ok((await TiposADtoAsync(db, [tipo], ct))[0]);
    }

    private static async Task<IResult> BorrarTipoAsync(Guid flujoId, Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (await db.Set<Familia>().AnyAsync(f => f.TipoDeFamiliaId == id, ct))
        {
            return ProblemResults.Conflict(http, "Este tipo ya tiene familias: desactívalo en vez de borrarlo, para que su historia siga diciendo qué eran.");
        }

        var borrados = await db.Set<TipoDeFamilia>().Where(t => t.Id == id && t.FlujoId == flujoId).ExecuteDeleteAsync(ct);
        return borrados == 0 ? ProblemResults.NotFound(http, "Tipo de familia no encontrado.") : Results.NoContent();
    }

    private static async Task<IResult?> ComprobarTipoAsync(Guid flujoId, Guid? id, GuardarTipoDeFamiliaRequest request, AppDbContext db, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>();
        void Error(string campo, string mensaje) => errores[campo] = [.. errores.GetValueOrDefault(campo, []), mensaje];

        var nombre = request.Nombre?.Trim() ?? string.Empty;
        if (nombre.Length is 0 or > 100) Error("nombre", "El nombre es obligatorio, de 100 letras como mucho.");
        else if (await db.Set<TipoDeFamilia>().AnyAsync(t => t.FlujoId == flujoId && t.Id != id && t.Nombre.ToLower() == nombre.ToLower(), ct))
        {
            Error("nombre", "El proceso ya tiene un tipo de familia con ese nombre.");
        }

        if (request.Descripcion is { Length: > 500 }) Error("descripcion", "La descripción va de 500 letras como mucho.");
        if (request.Condicion is not null && !Enum.TryParse<CondicionDeCierre>(request.Condicion, true, out _))
        {
            Error("condicion", "La condición es Siempre o SoloSiTodosBien.");
        }

        if (request.CreadorAlTerminarId is { } creadorId && !await db.Set<CreadorDeCaso>().AnyAsync(c => c.Id == creadorId, ct))
        {
            Error("creadorAlTerminarId", "Ese creador de casos no existe.");
        }

        return errores.Count == 0 ? null : Results.ValidationProblem(errores);
    }

    private static void AplicarTipo(TipoDeFamilia tipo, GuardarTipoDeFamiliaRequest request, DateTimeOffset ahora)
    {
        tipo.Nombre = request.Nombre.Trim();
        tipo.Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
        tipo.Activo = request.Activo;
        tipo.CreadorAlTerminarId = request.CreadorAlTerminarId;
        tipo.Condicion = Enum.TryParse<CondicionDeCierre>(request.Condicion, true, out var condicion) ? condicion : CondicionDeCierre.Siempre;
        tipo.UpdatedAt = ahora;
    }

    private static async Task<List<TipoDeFamiliaDto>> TiposADtoAsync(AppDbContext db, IReadOnlyList<TipoDeFamilia> tipos, CancellationToken ct)
    {
        var creadorIds = tipos.Where(t => t.CreadorAlTerminarId != null).Select(t => t.CreadorAlTerminarId!.Value).Distinct().ToList();
        var creadores = await db.Set<CreadorDeCaso>().AsNoTracking().Where(c => creadorIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Nombre, Proceso = c.Flujo!.Nombre }).ToDictionaryAsync(c => c.Id, ct);
        return tipos.Select(t =>
        {
            var creador = t.CreadorAlTerminarId is { } cid && creadores.TryGetValue(cid, out var c) ? c : null;
            return new TipoDeFamiliaDto(t.Id, t.FlujoId, t.Nombre, t.Descripcion, t.Activo, t.CreadorAlTerminarId, creador?.Nombre, creador?.Proceso,
                t.Condicion.ToString(), t.CreatedAt, t.UpdatedAt);
        }).ToList();
    }

    // ── Families ─────────────────────────────────────────────────────────────────────────────────────────────

    private static async Task<IResult> ListarAsync(
        Guid? flujoId, string? estado, string? search, Guid? tipoDeFamiliaId, int? page, int? pageSize, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var pagina = page is null or < 1 ? 1 : page.Value;
        var tamano = pageSize is null or < 1 or > 100 ? 20 : pageSize.Value;

        var flujos = await FlujoAccessAuthorization.FlujosAsignadosAsync(db, http.User.GetUserId(), ct);
        var consulta = db.Set<Familia>().AsNoTracking().Where(f => flujos.Contains(f.FlujoId));
        if (flujoId is { } fid) consulta = consulta.Where(f => f.FlujoId == fid);
        if (tipoDeFamiliaId is { } tid) consulta = consulta.Where(f => f.TipoDeFamiliaId == tid);
        var estados = (estado ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => Enum.TryParse<EstadoDeFamilia>(e, true, out var x) ? (EstadoDeFamilia?)x : null).Where(e => e is not null).Select(e => e!.Value).ToArray();
        if (estados.Length > 0) consulta = consulta.Where(f => estados.Contains(f.Estado));
        if (!string.IsNullOrWhiteSpace(search)) consulta = consulta.Where(f => f.Nombre.Contains(search.Trim()));

        var total = await consulta.CountAsync(ct);
        var filas = await Proyectar(consulta.OrderByDescending(f => f.CreatedAt).Skip((pagina - 1) * tamano).Take(tamano), db).ToListAsync(ct);
        return Results.Ok(new PagedResult<FamiliaDto>(filas, pagina, tamano, total));
    }

    private static async Task<IResult> ObtenerAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var flujos = await FlujoAccessAuthorization.FlujosAsignadosAsync(db, http.User.GetUserId(), ct);
        var familia = await Proyectar(db.Set<Familia>().AsNoTracking().Where(f => f.Id == id && flujos.Contains(f.FlujoId)), db).FirstOrDefaultAsync(ct);
        return familia is null ? ProblemResults.NotFound(http, "Familia no encontrada.") : Results.Ok(familia);
    }

    private static async Task<IResult> EventosAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (await FlujoDeLaFamiliaVisibleAsync(id, db, http, ct) is null) return ProblemResults.NotFound(http, "Familia no encontrada.");

        var eventos = await db.Set<FamiliaEvento>().AsNoTracking().Where(e => e.FamiliaId == id).OrderBy(e => e.OccurredAt)
            .Select(e => new FamiliaEventoDto(e.Id, e.Accion.ToString(), e.DetalleJson, e.OccurredAt)).ToListAsync(ct);
        return Results.Ok(eventos);
    }

    private static async Task<IResult> DeUnCasoAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var flujos = await FlujoAccessAuthorization.FlujosAsignadosAsync(db, http.User.GetUserId(), ct);
        var familias = db.Set<Familia>().AsNoTracking()
            .Where(f => flujos.Contains(f.FlujoId) && db.Set<MiembroDeFamilia>().Any(m => m.FamiliaId == f.Id && m.CasoId == id))
            .OrderByDescending(f => f.CreatedAt);
        return Results.Ok(await Proyectar(familias, db).ToListAsync(ct));
    }

    private static async Task<IResult> CrearAsync(CrearFamiliaRequest request, AppDbContext db, GestorDeFamilias gestor, HttpContext http, CancellationToken ct)
    {
        if (!await FlujoAccessAuthorization.TieneAccesoAlFlujoAsync(db, http.User.GetUserId(), request.FlujoId, ct)) return ProblemResults.NotFound(http, "Proceso no encontrado.");

        try
        {
            var familia = await gestor.CrearAsync(
                request.FlujoId, request.TipoDeFamiliaId.ToString(), request.Nombre, request.ParticipantesEsperados, http.User.GetUserId(), null, ct);
            await db.SaveChangesAsync(ct);
            if (request.CasoIds is { Count: > 0 } casos)
            {
                await gestor.AnadirAsync(familia.Id, await CasosDelProcesoAsync(db, casos, request.FlujoId, ct), request.FlujoId, ct);
                await db.SaveChangesAsync(ct);
            }

            return await ObtenerAsync(familia.Id, db, http, ct);
        }
        catch (FamiliaNoValidaException ex)
        {
            return ProblemResults.Conflict(http, ex.Message);
        }
    }

    private static async Task<IResult> CerrarAsync(Guid id, AppDbContext db, GestorDeFamilias gestor, HttpContext http, CancellationToken ct)
    {
        if (await FlujoDeLaFamiliaVisibleAsync(id, db, http, ct) is not { } flujoId) return ProblemResults.NotFound(http, "Familia no encontrada.");

        await gestor.CerrarAsync(id, flujoId, ct);
        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, db, http, ct);
    }

    private static async Task<IResult> AnadirCasosAsync(
        Guid id, AnadirCasosAFamiliaRequest request, AppDbContext db, GestorDeFamilias gestor, HttpContext http, CancellationToken ct)
    {
        if (await FlujoDeLaFamiliaVisibleAsync(id, db, http, ct) is not { } flujoId) return ProblemResults.NotFound(http, "Familia no encontrada.");
        if (request.CasoIds is not { Count: > 0 and <= 1000 }) return ProblemResults.Conflict(http, "Indica entre 1 y 1000 casos.");

        var casos = await CasosDelProcesoAsync(db, request.CasoIds, flujoId, ct);
        if (casos.Count != request.CasoIds.Distinct().Count()) return ProblemResults.Conflict(http, "Algún caso no existe o es de otro proceso.");

        try
        {
            await gestor.AnadirAsync(id, casos, flujoId, ct);
        }
        catch (FamiliaNoValidaException ex)
        {
            return ProblemResults.Conflict(http, ex.Message);
        }

        await db.SaveChangesAsync(ct);
        return await ObtenerAsync(id, db, http, ct);
    }

    private static Task<List<Guid>> CasosDelProcesoAsync(AppDbContext db, IReadOnlyList<Guid> ids, Guid flujoId, CancellationToken ct) =>
        db.Set<Caso>().AsNoTracking().Where(c => ids.Contains(c.Id) && c.FlujoId == flujoId).Select(c => c.Id).ToListAsync(ct);

    private static async Task<Guid?> FlujoDeLaFamiliaVisibleAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var flujoId = await db.Set<Familia>().AsNoTracking().Where(f => f.Id == id).Select(f => (Guid?)f.FlujoId).FirstOrDefaultAsync(ct);
        return flujoId is { } fid && await FlujoAccessAuthorization.TieneAccesoAlFlujoAsync(db, http.User.GetUserId(), fid, ct) ? fid : null;
    }

    /// <summary>One query for the page: the progress is counted in the database, not by loading the members.</summary>
    private static IQueryable<FamiliaDto> Proyectar(IQueryable<Familia> familias, AppDbContext db) =>
        familias.Select(f => new FamiliaDto(
            f.Id, f.FlujoId,
            db.Set<Flujo>().Where(x => x.Id == f.FlujoId).Select(x => x.Nombre).FirstOrDefault() ?? string.Empty,
            f.TipoDeFamiliaId, f.Tipo!.Nombre, f.Nombre, f.Estado.ToString(), f.ParticipantesEsperados,
            db.Set<MiembroDeFamilia>().Count(m => m.FamiliaId == f.Id),
            db.Set<MiembroDeFamilia>().Where(m => m.FamiliaId == f.Id).Join(db.Set<Caso>(), m => m.CasoId, c => c.Id, (m, c) => c)
                .Count(c => EstadosFinales.Contains(c.Estado)),
            f.Completados, f.Fallidos, f.Cancelados, f.CasoDeCierreId,
            db.Set<Caso>().Where(c => c.Id == f.CasoDeCierreId).Select(c => c.Titulo).FirstOrDefault(),
            f.NotaDeCierre, f.CreatedAt, f.CerradaAt, f.TerminadaAt));
}
