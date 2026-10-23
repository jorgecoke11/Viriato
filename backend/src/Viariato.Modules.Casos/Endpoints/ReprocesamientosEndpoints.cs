using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Flujos.Domain;
using Viariato.Shared;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

public sealed record ReglaDeReprocesamientoDto(
    Guid Id, Guid FlujoId, string Nombre, bool Activa, int Orden, Guid? EstadoNegocioId, string? EstadoNegocioNombre, string? EstadoEjecucion,
    string? PasoNombre, int EsperaMinutos, int? Limite, Guid? EstadoFinalId, string? EstadoFinalNombre, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record GuardarReglaDeReprocesamientoRequest(
    string Nombre, bool Activa, int Orden, Guid? EstadoNegocioId, string? EstadoEjecucion, string? PasoNombre, int EsperaMinutos, int? Limite,
    Guid? EstadoFinalId);

public sealed record ReprocesamientoProgramadoDto(
    Guid Id, Guid? ReglaId, string ReglaNombre, Guid CasoId, string CasoTitulo, string? PasoNombre, int Numero, DateTimeOffset ProgramadoPara,
    string Estado, string? Error, DateTimeOffset CreatedAt, DateTimeOffset? HechoAt);

/// <summary>The reprocessing rules of a process (who configures the process configures them), and the reprocessings they decided.</summary>
internal static class ReprocesamientosEndpoints
{
    /// <summary>How a case's last step can have ended when the case ends — the only ones a rule can ask for.</summary>
    private static readonly EjecucionPasoEstado[] EstadosFinalesDelPaso = [EjecucionPasoEstado.Completado, EjecucionPasoEstado.Fallido, EjecucionPasoEstado.Cancelado];

    public static void MapReprocesamientosEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/reprocesamientos").RequireAuthorization(Permissions.FlujosManage);
        group.MapGet("/", ListarAsync);
        group.MapPost("/", CrearAsync);
        group.MapPut("/{id:guid}", ActualizarAsync);
        group.MapDelete("/{id:guid}", BorrarAsync);
        group.MapGet("/pasos", PasosAsync);
        group.MapGet("/historial", HistorialAsync);
    }

    private static async Task<IResult> ListarAsync(Guid flujoId, AppDbContext db, CancellationToken ct)
    {
        var reglas = await db.Set<ReglaDeReprocesamiento>().AsNoTracking().Where(r => r.FlujoId == flujoId)
            .OrderBy(r => r.Orden).ThenBy(r => r.CreatedAt).ToListAsync(ct);
        var estados = await EstadosAsync(db, flujoId, ct);
        return Results.Ok(reglas.Select(r => ADto(r, estados)));
    }

    private static async Task<IResult> CrearAsync(Guid flujoId, GuardarReglaDeReprocesamientoRequest request, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (!await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct)) return ProblemResults.NotFound(http, "Proceso no encontrado.");
        if (await ComprobarAsync(flujoId, request, db, ct) is { } incorrecta) return incorrecta;

        var ahora = DateTimeOffset.UtcNow;
        var regla = new ReglaDeReprocesamiento { FlujoId = flujoId, Nombre = string.Empty, CreatedAt = ahora };
        Aplicar(regla, request, ahora);
        db.Add(regla);
        await db.SaveChangesAsync(ct);
        return Results.Ok(ADto(regla, await EstadosAsync(db, flujoId, ct)));
    }

    private static async Task<IResult> ActualizarAsync(
        Guid flujoId, Guid id, GuardarReglaDeReprocesamientoRequest request, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var regla = await db.Set<ReglaDeReprocesamiento>().FirstOrDefaultAsync(r => r.Id == id && r.FlujoId == flujoId, ct);
        if (regla is null) return ProblemResults.NotFound(http, "Regla no encontrada.");
        if (await ComprobarAsync(flujoId, request, db, ct) is { } incorrecta) return incorrecta;

        Aplicar(regla, request, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return Results.Ok(ADto(regla, await EstadosAsync(db, flujoId, ct)));
    }

    private static async Task<IResult> BorrarAsync(Guid flujoId, Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var borradas = await db.Set<ReglaDeReprocesamiento>().Where(r => r.Id == id && r.FlujoId == flujoId).ExecuteDeleteAsync(ct);
        return borradas == 0 ? ProblemResults.NotFound(http, "Regla no encontrada.") : Results.NoContent();
    }

    /// <summary>The names of the process's steps a rule can reprocess: those of its active version (or, without one, of its latest).</summary>
    private static async Task<IResult> PasosAsync(Guid flujoId, AppDbContext db, CancellationToken ct)
    {
        var versionId = await db.Set<Flujo>().AsNoTracking().Where(f => f.Id == flujoId).Select(f => f.VersionActivaId).FirstOrDefaultAsync(ct)
            ?? await db.Set<FlujoVersion>().AsNoTracking().Where(v => v.FlujoId == flujoId)
                .OrderByDescending(v => v.NumeroVersion).Select(v => (Guid?)v.Id).FirstOrDefaultAsync(ct);
        var nombres = versionId is { } id
            ? await db.Set<FlujoPasoDef>().AsNoTracking().Where(p => p.FlujoVersionId == id).OrderBy(p => p.Orden).Select(p => p.Nombre).ToListAsync(ct)
            : [];
        return Results.Ok(nombres.Distinct().ToList());
    }

    private static async Task<IResult> HistorialAsync(Guid flujoId, string? estado, int? page, int? pageSize, AppDbContext db, CancellationToken ct)
    {
        var pagina = page is null or < 1 ? 1 : page.Value;
        var tamano = pageSize is null or < 1 or > 100 ? 20 : pageSize.Value;

        var consulta = db.Set<ReprocesamientoProgramado>().AsNoTracking().Where(p => p.FlujoId == flujoId);
        if (Enum.TryParse<EstadoDeReprocesamiento>(estado, true, out var filtro)) consulta = consulta.Where(p => p.Estado == filtro);

        var total = await consulta.CountAsync(ct);
        var filas = await consulta.OrderByDescending(p => p.CreatedAt).Skip((pagina - 1) * tamano).Take(tamano)
            .Join(db.Set<Caso>(), p => p.CasoId, c => c.Id, (p, c) => new { p, c.Titulo })
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<ReprocesamientoProgramadoDto>(
            filas.Select(x => new ReprocesamientoProgramadoDto(
                x.p.Id, x.p.ReglaId, x.p.ReglaNombre, x.p.CasoId, x.Titulo, x.p.PasoNombre, x.p.Numero, x.p.ProgramadoPara, x.p.Estado.ToString(),
                x.p.Error, x.p.CreatedAt, x.p.HechoAt)).ToList(),
            pagina, tamano, total));
    }

    private static async Task<IResult?> ComprobarAsync(Guid flujoId, GuardarReglaDeReprocesamientoRequest request, AppDbContext db, CancellationToken ct)
    {
        var errores = new Dictionary<string, string[]>();
        void Error(string campo, string mensaje) => errores[campo] = [.. errores.GetValueOrDefault(campo, []), mensaje];

        if (string.IsNullOrWhiteSpace(request.Nombre) || request.Nombre.Trim().Length > 100) Error("nombre", "El nombre es obligatorio, de 100 letras como mucho.");

        if (request.EstadoEjecucion is { } estadoEjecucion
            && (!Enum.TryParse<EjecucionPasoEstado>(estadoEjecucion, true, out var leido) || !EstadosFinalesDelPaso.Contains(leido)))
        {
            Error("estadoEjecucion", $"Estado de la ejecución desconocido. Los que hay: {string.Join(", ", EstadosFinalesDelPaso)}.");
        }

        if (request.EstadoNegocioId is null && request.EstadoEjecucion is null)
        {
            Error("estadoNegocioId", "Indica el estado de negocio, el de la ejecución o los dos.");
        }

        if (request.EstadoNegocioId is { } negocioId && !await db.Set<FlujoEstadoDef>().AnyAsync(e => e.Id == negocioId && e.FlujoId == flujoId, ct))
        {
            Error("estadoNegocioId", "Ese estado de negocio no es de este proceso.");
        }

        if (request.PasoNombre is { } pasoNombre && !await db.Set<FlujoPasoDef>()
                .AnyAsync(p => p.Nombre == pasoNombre.Trim() && db.Set<FlujoVersion>().Any(v => v.Id == p.FlujoVersionId && v.FlujoId == flujoId), ct))
        {
            Error("pasoNombre", "Ese paso no es de este proceso.");
        }

        if (request.EsperaMinutos is < 0 or > 10_080) Error("esperaMinutos", "La espera va de 0 a 10080 minutos (una semana).");

        if (request.Limite is { } limite)
        {
            if (limite is < 1 or > 100) Error("limite", "El límite va de 1 a 100 reprocesamientos.");
            if (request.EstadoFinalId is not { } finalId) Error("estadoFinalId", "Con límite, indica el estado de negocio en que se queda el caso al agotarlo.");
            else if (!await db.Set<FlujoEstadoDef>().AnyAsync(e => e.Id == finalId && e.FlujoId == flujoId, ct)) Error("estadoFinalId", "Ese estado de negocio no es de este proceso.");
        }

        return errores.Count == 0 ? null : Results.ValidationProblem(errores);
    }

    private static void Aplicar(ReglaDeReprocesamiento regla, GuardarReglaDeReprocesamientoRequest request, DateTimeOffset ahora)
    {
        regla.Nombre = request.Nombre.Trim();
        regla.Activa = request.Activa;
        regla.Orden = request.Orden;
        regla.EstadoNegocioId = request.EstadoNegocioId;
        regla.EstadoEjecucion = request.EstadoEjecucion is { } e ? Enum.Parse<EjecucionPasoEstado>(e, true) : null;
        regla.PasoNombre = string.IsNullOrWhiteSpace(request.PasoNombre) ? null : request.PasoNombre.Trim();
        regla.EsperaMinutos = request.EsperaMinutos;
        regla.Limite = request.Limite;
        // The final state only means something with a limit.
        regla.EstadoFinalId = request.Limite is null ? null : request.EstadoFinalId;
        regla.UpdatedAt = ahora;
    }

    private static Task<Dictionary<Guid, string>> EstadosAsync(AppDbContext db, Guid flujoId, CancellationToken ct) =>
        db.Set<FlujoEstadoDef>().AsNoTracking().Where(e => e.FlujoId == flujoId).ToDictionaryAsync(e => e.Id, e => e.Display, ct);

    private static ReglaDeReprocesamientoDto ADto(ReglaDeReprocesamiento r, IReadOnlyDictionary<Guid, string> estados) => new(
        r.Id, r.FlujoId, r.Nombre, r.Activa, r.Orden, r.EstadoNegocioId, r.EstadoNegocioId is { } n ? estados.GetValueOrDefault(n) : null,
        r.EstadoEjecucion?.ToString(), r.PasoNombre, r.EsperaMinutos, r.Limite, r.EstadoFinalId,
        r.EstadoFinalId is { } f ? estados.GetValueOrDefault(f) : null, r.CreatedAt, r.UpdatedAt);
}
