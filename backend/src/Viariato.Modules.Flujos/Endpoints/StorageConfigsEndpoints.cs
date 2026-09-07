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
/// Owns only the storage config (provider, credentials, bucket/path) and which Flujo uses which —
/// actually reading/writing evidencia/documento bytes against it is separate, future work. Mutations
/// are hand-written, not the generic Crud kit, because credentials must never round-trip back to a
/// client (see <see cref="Contracts.StorageConfigDto.HasCredentials"/>) and update is a partial patch
/// over a value object the kit's ApplyUpdate alone would make easy to get wrong silently.
/// </summary>
internal static class StorageConfigsEndpoints
{
    public static void MapStorageConfigsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapReadOnlyCrud("/api/v1/storage-configs", new ReadOnlyCrudResource<StorageConfig, StorageConfigDto>
        {
            Id = s => s.Id,
            ToDto = s => s.ToDto(),
            Search = (query, term) => query.Where(s => s.Nombre.ToLower().Contains(term.ToLower())),
            Authorize = (http, ct) => FlujosAuthorization.RequireClaimAsync(http, Permissions.FlujosRead, ct),
        });

        var manage = endpoints.MapGroup("/api/v1/storage-configs").RequireAuthorization(Permissions.FlujosManage);

        manage.MapPost("/", CreateAsync);
        manage.MapPatch("/{id:guid}", UpdateAsync);
        manage.MapDelete("/{id:guid}", DeleteAsync);
    }

    private static async Task<IResult> CreateAsync(
        CreateStorageConfigRequest request,
        CreateStorageConfigRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var nombreTomado = await db.Set<StorageConfig>().AnyAsync(s => s.Nombre == request.Nombre, ct);
        if (nombreTomado) return ProblemResults.Conflict(http, "Ya existe un almacenamiento con ese nombre.");

        var now = DateTimeOffset.UtcNow;
        var config = new StorageConfig
        {
            Nombre = request.Nombre,
            Proveedor = request.Proveedor,
            Endpoint = request.Endpoint,
            Region = request.Region,
            BucketName = request.BucketName,
            AccessKey = request.AccessKey,
            SecretKey = request.SecretKey,
            UsePathStyle = request.UsePathStyle,
            UseSsl = request.UseSsl,
            LocalPath = request.LocalPath,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Add(config);
        await db.SaveChangesAsync(ct);

        return Results.Ok(config.ToDto());
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateStorageConfigRequest request,
        UpdateStorageConfigRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var config = await db.Set<StorageConfig>().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (config is null) return ProblemResults.NotFound(http, "Almacenamiento no encontrado.");

        if (request.Nombre is not null && request.Nombre != config.Nombre)
        {
            var nombreTomado = await db.Set<StorageConfig>().AnyAsync(s => s.Id != id && s.Nombre == request.Nombre, ct);
            if (nombreTomado) return ProblemResults.Conflict(http, "Ya existe un almacenamiento con ese nombre.");
            config.Nombre = request.Nombre;
        }

        if (request.Endpoint is not null) config.Endpoint = request.Endpoint;
        if (request.Region is not null) config.Region = request.Region;
        if (request.BucketName is not null) config.BucketName = request.BucketName;
        if (request.AccessKey is not null) config.AccessKey = request.AccessKey;
        if (request.SecretKey is not null) config.SecretKey = request.SecretKey;
        if (request.UsePathStyle is not null) config.UsePathStyle = request.UsePathStyle.Value;
        if (request.UseSsl is not null) config.UseSsl = request.UseSsl.Value;
        if (request.LocalPath is not null) config.LocalPath = request.LocalPath;
        if (request.Activo is not null) config.Activo = request.Activo.Value;
        config.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Results.Ok(config.ToDto());
    }

    private static async Task<IResult> DeleteAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var config = await db.Set<StorageConfig>().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (config is null) return ProblemResults.NotFound(http, "Almacenamiento no encontrado.");

        var enUso = await db.Set<Flujo>().AnyAsync(f => f.StorageConfigId == id, ct);
        if (enUso) return ProblemResults.Conflict(http, "No se puede eliminar un almacenamiento asignado a procesos.");

        db.Remove(config);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }
}
