using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.RpaFleet.Auth;
using Viariato.Modules.RpaFleet.Contracts;
using Viariato.Modules.RpaFleet.Domain;
using Viariato.Modules.RpaFleet.Validation;
using Viariato.Shared;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.RpaFleet.Endpoints;

/// <summary>
/// Hand-written, not the generic Crud kit: creating/regenerating a key must hand back the raw value
/// exactly once (DespliegueConApiKeyDto) — DespliegueDto itself never carries anything but the
/// display prefix, so the kit's uniform ToDto-for-every-response shape doesn't fit.
/// </summary>
internal static class DespliguesEndpoints
{
    public static void MapDespliguesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/despliegues").RequireAuthorization(Permissions.RpaManage);

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:guid}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPatch("/{id:guid}", UpdateAsync);
        group.MapPost("/{id:guid}/regenerar-clave", RegenerarClaveAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
    }

    private static async Task<IResult> ListAsync(int? page, int? pageSize, AppDbContext db, CancellationToken ct)
    {
        var currentPage = page is null or < 1 ? 1 : page.Value;
        var currentPageSize = pageSize is null or < 1 or > 100 ? 20 : pageSize.Value;

        var query = db.Set<Despliegue>().AsNoTracking()
            .Include(d => d.Equipo).Include(d => d.Servicio)
            .OrderByDescending(d => d.CreatedAt);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((currentPage - 1) * currentPageSize).Take(currentPageSize).ToListAsync(ct);

        return Results.Ok(new PagedResult<DespliegueDto>(items.Select(d => d.ToDto()).ToList(), currentPage, currentPageSize, total));
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var despliegue = await db.Set<Despliegue>().AsNoTracking()
            .Include(d => d.Equipo).Include(d => d.Servicio)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        return despliegue is null ? ProblemResults.NotFound(http, "Despliegue no encontrado.") : Results.Ok(despliegue.ToDto());
    }

    private static async Task<IResult> CreateAsync(
        CreateDespliegueRequest request,
        CreateDespliegueRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var equipoExiste = await db.Set<Equipo>().AnyAsync(e => e.Id == request.EquipoId, ct);
        if (!equipoExiste) return ProblemResults.NotFound(http, "Equipo no encontrado.");

        var servicioExiste = await db.Set<Servicio>().AnyAsync(s => s.Id == request.ServicioId, ct);
        if (!servicioExiste) return ProblemResults.NotFound(http, "Servicio no encontrado.");

        var yaExiste = await db.Set<Despliegue>().AnyAsync(d =>
            d.EquipoId == request.EquipoId && d.ServicioId == request.ServicioId && d.FlujoId == request.FlujoId, ct);
        if (yaExiste) return ProblemResults.Conflict(http, "Ya existe un despliegue de ese servicio en ese equipo para ese proceso.");

        var (rawKey, hash, prefix) = ApiKeyGenerator.Generate();
        var now = DateTimeOffset.UtcNow;
        var despliegue = new Despliegue
        {
            EquipoId = request.EquipoId,
            ServicioId = request.ServicioId,
            FlujoId = request.FlujoId,
            ApiKeyHash = hash,
            ApiKeyPrefix = prefix,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Add(despliegue);
        await db.SaveChangesAsync(ct);

        var creado = await db.Set<Despliegue>().Include(d => d.Equipo).Include(d => d.Servicio).FirstAsync(d => d.Id == despliegue.Id, ct);
        return Results.Ok(new DespliegueConApiKeyDto(creado.ToDto(), rawKey));
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, UpdateDespliegueRequest request, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var despliegue = await db.Set<Despliegue>().Include(d => d.Equipo).Include(d => d.Servicio).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (despliegue is null) return ProblemResults.NotFound(http, "Despliegue no encontrado.");

        if (request.Encendido is not null) despliegue.Encendido = request.Encendido.Value;
        despliegue.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Results.Ok(despliegue.ToDto());
    }

    private static async Task<IResult> RegenerarClaveAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var despliegue = await db.Set<Despliegue>().Include(d => d.Equipo).Include(d => d.Servicio).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (despliegue is null) return ProblemResults.NotFound(http, "Despliegue no encontrado.");

        var (rawKey, hash, prefix) = ApiKeyGenerator.Generate();
        despliegue.ApiKeyHash = hash;
        despliegue.ApiKeyPrefix = prefix;
        despliegue.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Results.Ok(new DespliegueConApiKeyDto(despliegue.ToDto(), rawKey));
    }

    private static async Task<IResult> DeleteAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var despliegue = await db.Set<Despliegue>().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (despliegue is null) return ProblemResults.NotFound(http, "Despliegue no encontrado.");

        db.Remove(despliegue);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
