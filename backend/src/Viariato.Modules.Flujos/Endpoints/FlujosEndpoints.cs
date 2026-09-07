using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Infrastructure.Crud;
using Viariato.Modules.Flujos.Contracts;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Flujos.Validation;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Flujos.Endpoints;

/// <summary>
/// Reads go through the generic Crud kit (list/get are genuinely CRUD-shaped for Flujo master data);
/// mutations are hand-written because "create/update/delete a Flujo" carries real domain rules
/// (name uniqueness, "can't delete once it has versions") that the kit's hooks alone would obscure.
/// </summary>
internal static class FlujosEndpoints
{
    public static void MapFlujosCrudEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapReadOnlyCrud("/api/v1/flujos", new ReadOnlyCrudResource<Flujo, FlujoDto>
        {
            Id = f => f.Id,
            Include = q => q.Include(f => f.VersionActiva).Include(f => f.StorageConfig),
            ToDto = f => f.ToDto(),
            Search = (query, term) => query.Where(f => f.Nombre.ToLower().Contains(term.ToLower())),
            Authorize = (http, ct) => FlujosAuthorization.RequireClaimAsync(http, Permissions.FlujosRead, ct),
        });

        var manage = endpoints.MapGroup("/api/v1/flujos").RequireAuthorization(Permissions.FlujosManage);

        manage.MapPost("/", CreateFlujoAsync);
        manage.MapPatch("/{id:guid}", UpdateFlujoAsync);
        manage.MapDelete("/{id:guid}", DeleteFlujoAsync);
        manage.MapPut("/{id:guid}/almacenamiento", UpdateFlujoAlmacenamientoAsync);
    }

    private static async Task<IResult> CreateFlujoAsync(
        CreateFlujoRequest request,
        CreateFlujoRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var nombreTomado = await db.Set<Flujo>().AnyAsync(f => f.Nombre == request.Nombre, ct);
        if (nombreTomado) return ProblemResults.Conflict(http, "Ya existe un flujo con ese nombre.");

        var now = DateTimeOffset.UtcNow;
        var flujo = new Flujo
        {
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Add(flujo);
        await db.SaveChangesAsync(ct);

        return Results.Ok(flujo.ToDto());
    }

    private static async Task<IResult> UpdateFlujoAsync(
        Guid id,
        UpdateFlujoRequest request,
        UpdateFlujoRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var flujo = await db.Set<Flujo>().Include(f => f.VersionActiva).Include(f => f.StorageConfig).FirstOrDefaultAsync(f => f.Id == id, ct);
        if (flujo is null) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        if (request.Nombre is not null && request.Nombre != flujo.Nombre)
        {
            var nombreTomado = await db.Set<Flujo>().AnyAsync(f => f.Id != id && f.Nombre == request.Nombre, ct);
            if (nombreTomado) return ProblemResults.Conflict(http, "Ya existe un flujo con ese nombre.");
            flujo.Nombre = request.Nombre;
        }

        if (request.Descripcion is not null) flujo.Descripcion = request.Descripcion;
        if (request.Activo is not null) flujo.Activo = request.Activo.Value;
        flujo.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Results.Ok(flujo.ToDto());
    }

    private static async Task<IResult> UpdateFlujoAlmacenamientoAsync(
        Guid id,
        UpdateFlujoAlmacenamientoRequest request,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var flujo = await db.Set<Flujo>().FirstOrDefaultAsync(f => f.Id == id, ct);
        if (flujo is null) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        if (request.StorageConfigId is not null)
        {
            var existe = await db.Set<StorageConfig>().AnyAsync(s => s.Id == request.StorageConfigId, ct);
            if (!existe) return ProblemResults.NotFound(http, "Almacenamiento no encontrado.");
        }

        flujo.StorageConfigId = request.StorageConfigId;
        flujo.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var actualizado = await db.Set<Flujo>()
            .Include(f => f.VersionActiva)
            .Include(f => f.StorageConfig)
            .FirstAsync(f => f.Id == id, ct);

        return Results.Ok(actualizado.ToDto());
    }

    private static async Task<IResult> DeleteFlujoAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var flujo = await db.Set<Flujo>().FirstOrDefaultAsync(f => f.Id == id, ct);
        if (flujo is null) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        var tieneVersiones = await db.Set<FlujoVersion>().AnyAsync(v => v.FlujoId == id, ct);
        if (tieneVersiones) return ProblemResults.Conflict(http, "No se puede eliminar un flujo que tiene versiones.");

        db.Remove(flujo);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }
}
