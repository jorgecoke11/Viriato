using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Flujos.Contracts;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Flujos.Validation;
using Viariato.Modules.RpaFleet.Domain;
using Viariato.Shared;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Flujos.Endpoints;

/// <summary>
/// Everything here is hand-written (no Crud kit) because versioning a Flujo is real domain behavior:
/// a version is immutable once it leaves Borrador, and publishing atomically swaps which version new
/// Casos will run — not a plain field update.
/// </summary>
internal static class FlujoVersionesEndpoints
{
    public static void MapFlujoVersionesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var read = endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/versiones").RequireAuthorization(Permissions.FlujosRead);
        read.MapGet("/", ListVersionesAsync);
        read.MapGet("/{versionId:guid}", GetVersionAsync);

        var manage = endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/versiones").RequireAuthorization(Permissions.FlujosManage);
        manage.MapPost("/", CreateVersionAsync);
        manage.MapPut("/{versionId:guid}/pasos", ReplacePasosAsync);
        manage.MapPost("/{versionId:guid}/publicar", PublicarVersionAsync);
        manage.MapPost("/{versionId:guid}/archivar", ArchivarVersionAsync);
    }

    private static async Task<IResult> ListVersionesAsync(Guid flujoId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var flujoExiste = await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct);
        if (!flujoExiste) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        var versiones = await db.Set<FlujoVersion>().AsNoTracking()
            .Where(v => v.FlujoId == flujoId)
            .OrderByDescending(v => v.NumeroVersion)
            .Select(v => v.ToDto())
            .ToListAsync(ct);

        return Results.Ok(versiones);
    }

    private static async Task<IResult> GetVersionAsync(Guid flujoId, Guid versionId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var version = await db.Set<FlujoVersion>().AsNoTracking()
            .Include(v => v.Pasos)
            .Include(v => v.Transiciones).ThenInclude(t => t.EstadoNegocio)
            .AsSplitQuery()
            .FirstOrDefaultAsync(v => v.Id == versionId && v.FlujoId == flujoId, ct);

        return version is null
            ? ProblemResults.NotFound(http, "Versión no encontrada.")
            : Results.Ok(version.ToDetailDto());
    }

    private static async Task<IResult> CreateVersionAsync(
        Guid flujoId,
        CreateFlujoVersionRequest request,
        CreateFlujoVersionRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var flujo = await db.Set<Flujo>().FirstOrDefaultAsync(f => f.Id == flujoId, ct);
        if (flujo is null) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        var maxNumero = await db.Set<FlujoVersion>()
            .Where(v => v.FlujoId == flujoId)
            .Select(v => (int?)v.NumeroVersion)
            .MaxAsync(ct) ?? 0;

        var version = new FlujoVersion
        {
            FlujoId = flujoId,
            NumeroVersion = maxNumero + 1,
            Estado = FlujoVersionEstado.Borrador,
            Notas = request.Notas,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Add(version);
        await db.SaveChangesAsync(ct);

        return Results.Ok(version.ToDto());
    }

    private static async Task<IResult> ReplacePasosAsync(
        Guid flujoId,
        Guid versionId,
        ReplacePasosRequest request,
        ReplacePasosRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var version = await db.Set<FlujoVersion>()
            .Include(v => v.Pasos)
            .Include(v => v.Transiciones)
            .AsSplitQuery()
            .FirstOrDefaultAsync(v => v.Id == versionId && v.FlujoId == flujoId, ct);
        if (version is null) return ProblemResults.NotFound(http, "Versión no encontrada.");

        if (version.Estado != FlujoVersionEstado.Borrador)
        {
            return ProblemResults.Conflict(http, "Solo se pueden modificar los pasos de una versión en Borrador.");
        }

        // The states the arrows name, created if the process does not have them yet — before anything else changes, since
        // creating one saves on its own.
        var estadosPorCodigo = new Dictionary<string, FlujoEstadoDef>(StringComparer.Ordinal);
        foreach (var codigo in (request.Transiciones ?? []).Select(t => t.EstadoNegocioCodigo?.Trim()).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            var (estado, _) = await EstadosDeNegocio.ObtenerOCrearAsync(db, flujoId, codigo, ct);
            if (estado is null) return ProblemResults.Conflict(http, $"El estado de negocio \"{codigo}\" está desactivado en este proceso.");
            estadosPorCodigo[codigo] = estado;
        }

        if (request.Pasos.Any(p => p.AgenteDefinicionId is not null))
        {
            var agenteIds = request.Pasos.Where(p => p.AgenteDefinicionId is not null).Select(p => p.AgenteDefinicionId!.Value).ToList();
            var agentesExistentes = await db.Set<AgenteDefinicion>().Where(a => agenteIds.Contains(a.Id)).Select(a => a.Id).ToListAsync(ct);
            if (agentesExistentes.Count != agenteIds.Distinct().Count())
            {
                return ProblemResults.Conflict(http, "Uno o más AgenteDefinicionId no existen.");
            }
        }

        if (request.Pasos.Any(p => p.ServicioId is not null))
        {
            var servicioIds = request.Pasos.Where(p => p.ServicioId is not null).Select(p => p.ServicioId!.Value).ToList();
            var serviciosExistentes = await db.Set<Servicio>().Where(s => servicioIds.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct);
            if (serviciosExistentes.Count != servicioIds.Distinct().Count())
            {
                return ProblemResults.Conflict(http, "Uno o más ServicioId no existen.");
            }
        }

        db.RemoveRange(version.Transiciones);
        version.Transiciones.Clear();
        db.RemoveRange(version.Pasos);
        version.Pasos.Clear();

        var conTransiciones = request.Transiciones is not null;

        // Added explicitly via db.Add (not version.Pasos.Add) — FlujoPasoDef.Id is assigned
        // client-side (Guid.CreateVersion7()), and adding to an already-tracked parent's navigation
        // collection makes EF's key-is-set heuristic assume these rows already exist, generating
        // UPDATEs that match zero rows instead of INSERTs.
        var nuevosPasos = request.Pasos.Select(input =>
        {
            var tipo = Enum.Parse<TipoPaso>(input.TipoPaso, ignoreCase: true);
            return new FlujoPasoDef
            {
                FlujoVersionId = version.Id,
                Orden = input.Orden,
                Nombre = input.Nombre,
                TipoPaso = tipo,
                AgenteDefinicionId = input.AgenteDefinicionId,
                ServicioId = input.ServicioId,
                // In the arrow-shaped flow a Rpa step is just its servicio: robots never read that configuration.
                ConfiguracionJson = conTransiciones && tipo == TipoPaso.Rpa ? null : input.ConfiguracionJson,
                PosicionX = input.PosicionX,
                PosicionY = input.PosicionY,
                MaximoDeVisitas = input.MaximoDeVisitas,
                CreatedAt = DateTimeOffset.UtcNow,
            };
        }).ToList();
        db.AddRange(nuevosPasos);
        version.Pasos = nuevosPasos;

        if (request.Transiciones is { } transiciones)
        {
            var porClave = request.Pasos.Zip(nuevosPasos).ToDictionary(par => par.First.Clave!.Trim(), par => par.Second, StringComparer.Ordinal);
            var ahora = DateTimeOffset.UtcNow;
            var nuevas = transiciones.Select((t, i) => new FlujoTransicionDef
            {
                FlujoVersionId = version.Id,
                PasoOrigenId = t.Origen is null ? null : porClave[t.Origen.Trim()].Id,
                Resultado = t.Origen is null ? ResultadoEjecucion.Completada : Enum.Parse<ResultadoEjecucion>(t.Resultado!, ignoreCase: true),
                EstadoNegocioId = t.EstadoNegocioCodigo is { } codigo ? estadosPorCodigo[codigo.Trim()].Id : null,
                EstadoNegocio = t.EstadoNegocioCodigo is { } c ? estadosPorCodigo[c.Trim()] : null,
                PasoDestinoId = porClave[t.Destino.Trim()].Id,
                // Kept in the order they were sent, which is how they are listed back.
                CreatedAt = ahora.AddTicks(i),
            }).ToList();
            db.AddRange(nuevas);
            version.Transiciones = nuevas;
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(version.ToDetailDto());
    }

    private static async Task<IResult> PublicarVersionAsync(Guid flujoId, Guid versionId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var flujo = await db.Set<Flujo>().FirstOrDefaultAsync(f => f.Id == flujoId, ct);
        if (flujo is null) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        var version = await db.Set<FlujoVersion>().Include(v => v.Pasos).Include(v => v.Transiciones).AsSplitQuery()
            .FirstOrDefaultAsync(v => v.Id == versionId && v.FlujoId == flujoId, ct);
        if (version is null) return ProblemResults.NotFound(http, "Versión no encontrada.");

        if (version.Estado != FlujoVersionEstado.Borrador)
        {
            return ProblemResults.Conflict(http, "Solo se puede publicar una versión en Borrador.");
        }

        if (version.Pasos.Count == 0)
        {
            return ProblemResults.Conflict(http, "No se puede publicar una versión sin pasos.");
        }

        // Once published, this version is where every active creator of the process opens its Casos, at the step it names: a creator
        // whose step is not here would stop working (it refuses every Caso). Better to say so now than when someone creates one.
        var creadores = await db.Set<CreadorDeCaso>().AsNoTracking()
            .Where(c => c.FlujoId == flujoId && c.Activo && c.PasoInicialNombre != null)
            .Select(c => new { c.Nombre, Paso = c.PasoInicialNombre! })
            .ToListAsync(ct);
        var nombresDePasos = version.Pasos.Select(p => p.Nombre.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sinPaso = creadores.Where(c => !nombresDePasos.Contains(c.Paso.Trim())).ToList();
        if (sinPaso.Count > 0)
        {
            return ProblemResults.Conflict(http,
                "Estos creadores empiezan en un paso que esta versión no tiene: "
                + string.Join("; ", sinPaso.Select(c => $"«{c.Nombre}» → «{c.Paso}»"))
                + ". Pon un paso con ese nombre o cambia el paso inicial del creador.");
        }

        // A published flow has no dead steps: every one must be reachable by some arrow from an entry — the start, or a step a creator
        // opens its Casos at. A process may hold several flows that way (e.g. one that only creates Casos, and the one they then follow).
        if (version.Transiciones.Count > 0)
        {
            var entradas = version.Pasos
                .Where(p => creadores.Any(c => string.Equals(c.Paso.Trim(), p.Nombre.Trim(), StringComparison.OrdinalIgnoreCase)))
                .Select(p => p.Id);
            var alcanzables = PasosAlcanzables(version.Transiciones, entradas);
            var muertos = version.Pasos.Where(p => !alcanzables.Contains(p.Id)).OrderBy(p => p.Orden).Select(p => p.Nombre).ToList();
            if (muertos.Count > 0)
            {
                return ProblemResults.Conflict(http,
                    $"Ninguna flecha ni ningún creador lleva a: {string.Join(", ", muertos)}. Únelos al flujo, haz que un creador empiece ahí o quítalos antes de publicar.");
            }
        }

        version.Estado = FlujoVersionEstado.Publicada;
        version.PublishedAt = DateTimeOffset.UtcNow;
        flujo.VersionActivaId = version.Id;
        flujo.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Results.Ok(version.ToDto());
    }

    private static async Task<IResult> ArchivarVersionAsync(Guid flujoId, Guid versionId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var flujo = await db.Set<Flujo>().FirstOrDefaultAsync(f => f.Id == flujoId, ct);
        if (flujo is null) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        var version = await db.Set<FlujoVersion>().FirstOrDefaultAsync(v => v.Id == versionId && v.FlujoId == flujoId, ct);
        if (version is null) return ProblemResults.NotFound(http, "Versión no encontrada.");

        if (flujo.VersionActivaId == version.Id)
        {
            return ProblemResults.Conflict(http, "No se puede archivar la versión activa del flujo.");
        }

        version.Estado = FlujoVersionEstado.Archivada;
        await db.SaveChangesAsync(ct);

        return Results.Ok(version.ToDto());
    }

    /// <summary>The steps an arrow path reaches from the start or from one of <paramref name="entradas"/> (steps Casos start at
    /// directly, such as a creator's first step), those included.</summary>
    internal static HashSet<Guid> PasosAlcanzables(IEnumerable<FlujoTransicionDef> transiciones, IEnumerable<Guid>? entradas = null)
    {
        var lista = transiciones.ToList();
        var alcanzables = new HashSet<Guid>();
        var pendientes = new Queue<Guid>(lista.Where(t => t.PasoOrigenId is null).Select(t => t.PasoDestinoId).Concat(entradas ?? []));
        while (pendientes.TryDequeue(out var paso))
        {
            if (!alcanzables.Add(paso)) continue;
            foreach (var siguiente in lista.Where(t => t.PasoOrigenId == paso)) pendientes.Enqueue(siguiente.PasoDestinoId);
        }

        return alcanzables;
    }
}
