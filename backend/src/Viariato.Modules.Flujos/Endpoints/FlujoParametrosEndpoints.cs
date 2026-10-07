using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Flujos.Contracts;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Flujos.Validation;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Flujos.Endpoints;

/// <summary>
/// Per-process settings the robots read at run time (see <see cref="FlujoParametro"/>). Codigo is never
/// editable once created — a robot asks for it by name — but unlike business states a parameter that is no
/// longer wanted can simply be deleted: nothing else references it.
/// </summary>
internal static class FlujoParametrosEndpoints
{
    public static void MapFlujoParametrosEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/parametros")
            .RequireAuthorization(Permissions.FlujosRead)
            .MapGet("/", ListAsync);

        var manage = endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/parametros").RequireAuthorization(Permissions.FlujosManage);
        manage.MapPost("/", CreateAsync);
        manage.MapPatch("/{id:guid}", UpdateAsync);
        manage.MapDelete("/{id:guid}", DeleteAsync);
    }

    private static async Task<IResult> ListAsync(Guid flujoId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (!await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct)) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        var parametros = await db.Set<FlujoParametro>().AsNoTracking()
            .Where(p => p.FlujoId == flujoId)
            .OrderBy(p => p.Codigo)
            .ToListAsync(ct);

        return Results.Ok(parametros.Select(p => p.ToDto()).ToList());
    }

    private static async Task<IResult> CreateAsync(
        Guid flujoId,
        CreateFlujoParametroRequest request,
        CreateFlujoParametroRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        if (!await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct)) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        if (await db.Set<FlujoParametro>().AnyAsync(p => p.FlujoId == flujoId && p.Codigo == request.Codigo, ct))
        {
            return ProblemResults.Conflict(http, "Ya existe un parámetro con ese código en este proceso.");
        }

        var now = DateTimeOffset.UtcNow;
        var parametro = new FlujoParametro
        {
            FlujoId = flujoId,
            Codigo = request.Codigo,
            Valor = request.Valor,
            Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Add(parametro);
        await db.SaveChangesAsync(ct);
        return Results.Ok(parametro.ToDto());
    }

    private static async Task<IResult> UpdateAsync(
        Guid flujoId,
        Guid id,
        UpdateFlujoParametroRequest request,
        UpdateFlujoParametroRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var parametro = await db.Set<FlujoParametro>().FirstOrDefaultAsync(p => p.Id == id && p.FlujoId == flujoId, ct);
        if (parametro is null) return ProblemResults.NotFound(http, "Parámetro no encontrado.");

        parametro.Valor = request.Valor;
        parametro.Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
        parametro.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Results.Ok(parametro.ToDto());
    }

    private static async Task<IResult> DeleteAsync(Guid flujoId, Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var parametro = await db.Set<FlujoParametro>().FirstOrDefaultAsync(p => p.Id == id && p.FlujoId == flujoId, ct);
        if (parametro is null) return ProblemResults.NotFound(http, "Parámetro no encontrado.");

        db.Remove(parametro);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
