using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Flujos.Contracts;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Flujos.Esquemas;
using Viariato.Modules.Flujos.Validation;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Flujos.Endpoints;

/// <summary>
/// The configuration side of creators: whoever manages the Flujo decides what each one starts (type, service, estado, title).
/// Using them to create Casos is in Casos — it needs the orchestrator. Unlike types and estados, a creator can be deleted:
/// no Caso points at it, it only decided how that Caso was born.
/// </summary>
internal static class CreadoresDeCasoEndpoints
{
    public static void MapCreadoresDeCasoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/flujos/{flujoId:guid}/creadores", ListAsync).RequireAuthorization(Permissions.FlujosRead);

        var manage = endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/creadores").RequireAuthorization(Permissions.FlujosCreadores);
        manage.MapPost("/", CreateAsync);
        manage.MapPut("/{id:guid}", ReplaceAsync);
        manage.MapDelete("/{id:guid}", DeleteAsync);
    }

    private static async Task<IResult> ListAsync(Guid flujoId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (!await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct)) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        var creadores = await db.Set<CreadorDeCaso>().AsNoTracking()
            .Include(c => c.TipoCaso).Include(c => c.EstadoNegocioInicial)
            .Where(c => c.FlujoId == flujoId)
            .OrderBy(c => c.Orden).ThenBy(c => c.Nombre)
            .ToListAsync(ct);

        return Results.Ok(creadores.Select(c => c.ToDto()).ToList());
    }

    private static async Task<IResult> CreateAsync(
        Guid flujoId, GuardarCreadorDeCasoRequest request, GuardarCreadorDeCasoRequestValidator validator,
        AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var flujo = await db.Set<Flujo>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == flujoId, ct);
        if (flujo is null) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        if (await ComprobarReferenciasAsync(db, http, flujo, request, ct) is { } rechazo) return rechazo;
        if (await db.Set<CreadorDeCaso>().AnyAsync(c => c.FlujoId == flujoId && c.Nombre == request.Nombre, ct))
        {
            return ProblemResults.Conflict(http, "Ya existe un creador con ese nombre en este proceso.");
        }

        var now = DateTimeOffset.UtcNow;
        var creador = new CreadorDeCaso { FlujoId = flujoId, Nombre = request.Nombre, PlantillaTitulo = PlantillaDeTitulo.Predeterminada, CreatedAt = now, UpdatedAt = now };
        Aplicar(creador, request);

        db.Add(creador);
        await db.SaveChangesAsync(ct);

        return Results.Ok(await CargarAsync(db, creador.Id, ct));
    }

    private static async Task<IResult> ReplaceAsync(
        Guid flujoId, Guid id, GuardarCreadorDeCasoRequest request, GuardarCreadorDeCasoRequestValidator validator,
        AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var creador = await db.Set<CreadorDeCaso>().FirstOrDefaultAsync(c => c.Id == id && c.FlujoId == flujoId, ct);
        if (creador is null) return ProblemResults.NotFound(http, "Creador no encontrado.");

        var flujo = await db.Set<Flujo>().AsNoTracking().FirstAsync(f => f.Id == flujoId, ct);
        if (await ComprobarReferenciasAsync(db, http, flujo, request, ct) is { } rechazo) return rechazo;
        if (await db.Set<CreadorDeCaso>().AnyAsync(c => c.FlujoId == flujoId && c.Nombre == request.Nombre && c.Id != id, ct))
        {
            return ProblemResults.Conflict(http, "Ya existe un creador con ese nombre en este proceso.");
        }

        Aplicar(creador, request);
        creador.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(await CargarAsync(db, creador.Id, ct));
    }

    private static async Task<IResult> DeleteAsync(Guid flujoId, Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var creador = await db.Set<CreadorDeCaso>().FirstOrDefaultAsync(c => c.Id == id && c.FlujoId == flujoId, ct);
        if (creador is null) return ProblemResults.NotFound(http, "Creador no encontrado.");

        db.Remove(creador);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static void Aplicar(CreadorDeCaso creador, GuardarCreadorDeCasoRequest request)
    {
        creador.Nombre = request.Nombre;
        creador.Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
        creador.TipoCasoId = request.TipoCasoId;
        creador.PasoInicialNombre = string.IsNullOrWhiteSpace(request.PasoInicialNombre) ? null : request.PasoInicialNombre.Trim();
        creador.EstadoNegocioInicialId = request.EstadoNegocioInicialId;
        creador.PlantillaTitulo = string.IsNullOrWhiteSpace(request.PlantillaTitulo) ? PlantillaDeTitulo.Predeterminada : request.PlantillaTitulo.Trim();
        creador.Orden = request.Orden;
        creador.Activo = request.Activo;
    }

    /// <summary>The type, the estado and the step have to belong to this Flujo — otherwise the creator would only fail later,
    /// in front of a user who cannot fix it.</summary>
    private static async Task<IResult?> ComprobarReferenciasAsync(
        AppDbContext db, HttpContext http, Flujo flujo, GuardarCreadorDeCasoRequest request, CancellationToken ct)
    {
        if (request.TipoCasoId is { } tipoId &&
            !await db.Set<FlujoTipoCasoDef>().AnyAsync(t => t.Id == tipoId && t.FlujoId == flujo.Id && t.Activo, ct))
        {
            return ProblemResults.Conflict(http, "El tipo de caso indicado no existe en este proceso o está desactivado.");
        }

        if (request.EstadoNegocioInicialId is { } estadoId &&
            !await db.Set<FlujoEstadoDef>().AnyAsync(e => e.Id == estadoId && e.FlujoId == flujo.Id && e.Activo, ct))
        {
            return ProblemResults.Conflict(http, "El estado de negocio indicado no existe en este proceso o está desactivado.");
        }

        // Only checkable once there is a published version to look the step up in; the creator is useless until then anyway.
        if (!string.IsNullOrWhiteSpace(request.PasoInicialNombre) && flujo.VersionActivaId is { } versionId)
        {
            var nombre = request.PasoInicialNombre.Trim();
            if (!await db.Set<FlujoPasoDef>().AnyAsync(p => p.FlujoVersionId == versionId && p.Nombre == nombre, ct))
            {
                return ProblemResults.Conflict(http, $"La versión activa del proceso no tiene ningún paso llamado «{nombre}».");
            }
        }

        return null;
    }

    private static async Task<CreadorDeCasoDto> CargarAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        (await db.Set<CreadorDeCaso>().AsNoTracking()
            .Include(c => c.TipoCaso).Include(c => c.EstadoNegocioInicial)
            .FirstAsync(c => c.Id == id, ct)).ToDto();
}
