using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Flujos.Contracts;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Flujos.Validation;
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
            .FirstOrDefaultAsync(v => v.Id == versionId && v.FlujoId == flujoId, ct);
        if (version is null) return ProblemResults.NotFound(http, "Versión no encontrada.");

        if (version.Estado != FlujoVersionEstado.Borrador)
        {
            return ProblemResults.Conflict(http, "Solo se pueden modificar los pasos de una versión en Borrador.");
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

        db.RemoveRange(version.Pasos);
        version.Pasos.Clear();

        // Added explicitly via db.Add (not version.Pasos.Add) — FlujoPasoDef.Id is assigned
        // client-side (Guid.CreateVersion7()), and adding to an already-tracked parent's navigation
        // collection makes EF's key-is-set heuristic assume these rows already exist, generating
        // UPDATEs that match zero rows instead of INSERTs.
        var nuevosPasos = request.Pasos.Select(input => new FlujoPasoDef
        {
            FlujoVersionId = version.Id,
            Orden = input.Orden,
            Nombre = input.Nombre,
            TipoPaso = Enum.Parse<TipoPaso>(input.TipoPaso, ignoreCase: true),
            AgenteDefinicionId = input.AgenteDefinicionId,
            ConfiguracionJson = input.ConfiguracionJson,
            CreatedAt = DateTimeOffset.UtcNow,
        }).ToList();
        db.AddRange(nuevosPasos);
        version.Pasos = nuevosPasos;

        await db.SaveChangesAsync(ct);

        return Results.Ok(version.ToDetailDto());
    }

    private static async Task<IResult> PublicarVersionAsync(Guid flujoId, Guid versionId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var flujo = await db.Set<Flujo>().FirstOrDefaultAsync(f => f.Id == flujoId, ct);
        if (flujo is null) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        var version = await db.Set<FlujoVersion>().Include(v => v.Pasos).FirstOrDefaultAsync(v => v.Id == versionId && v.FlujoId == flujoId, ct);
        if (version is null) return ProblemResults.NotFound(http, "Versión no encontrada.");

        if (version.Estado != FlujoVersionEstado.Borrador)
        {
            return ProblemResults.Conflict(http, "Solo se puede publicar una versión en Borrador.");
        }

        if (version.Pasos.Count == 0)
        {
            return ProblemResults.Conflict(http, "No se puede publicar una versión sin pasos.");
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
}
