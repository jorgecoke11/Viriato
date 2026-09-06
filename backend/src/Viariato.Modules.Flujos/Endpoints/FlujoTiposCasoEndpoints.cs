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
/// Case categories the user defines per Flujo (e.g. "Alta simple" vs "Alta compleja") — chosen once
/// when a Caso is created, orthogonal to its business estado. No delete, same reasoning as
/// FlujoEstadoDef: a Caso may already point at one, so retiring it is Activo=false, not removal.
/// </summary>
internal static class FlujoTiposCasoEndpoints
{
    public static void MapFlujoTiposCasoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var read = endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/tipos-caso").RequireAuthorization(Permissions.FlujosRead);
        read.MapGet("/", ListTiposAsync);

        var manage = endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/tipos-caso").RequireAuthorization(Permissions.FlujosManage);
        manage.MapPost("/", CreateTipoAsync);
        manage.MapPatch("/{id:guid}", UpdateTipoAsync);
    }

    private static async Task<IResult> ListTiposAsync(Guid flujoId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var flujoExiste = await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct);
        if (!flujoExiste) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        var tipos = await db.Set<FlujoTipoCasoDef>().AsNoTracking()
            .Where(t => t.FlujoId == flujoId)
            .OrderBy(t => t.Orden)
            .Select(t => t.ToDto())
            .ToListAsync(ct);

        return Results.Ok(tipos);
    }

    private static async Task<IResult> CreateTipoAsync(
        Guid flujoId,
        CreateFlujoTipoCasoRequest request,
        CreateFlujoTipoCasoRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var flujoExiste = await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct);
        if (!flujoExiste) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        var nombreTomado = await db.Set<FlujoTipoCasoDef>().AnyAsync(t => t.FlujoId == flujoId && t.Nombre == request.Nombre, ct);
        if (nombreTomado) return ProblemResults.Conflict(http, "Ya existe un tipo de caso con ese nombre en este flujo.");

        var now = DateTimeOffset.UtcNow;
        var tipo = new FlujoTipoCasoDef
        {
            FlujoId = flujoId,
            Nombre = request.Nombre,
            Orden = request.Orden,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Add(tipo);
        await db.SaveChangesAsync(ct);

        return Results.Ok(tipo.ToDto());
    }

    private static async Task<IResult> UpdateTipoAsync(
        Guid flujoId,
        Guid id,
        UpdateFlujoTipoCasoRequest request,
        UpdateFlujoTipoCasoRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var tipo = await db.Set<FlujoTipoCasoDef>().FirstOrDefaultAsync(t => t.Id == id && t.FlujoId == flujoId, ct);
        if (tipo is null) return ProblemResults.NotFound(http, "Tipo de caso no encontrado.");

        if (request.Nombre is not null) tipo.Nombre = request.Nombre;
        if (request.Orden is not null) tipo.Orden = request.Orden.Value;
        if (request.Activo is not null) tipo.Activo = request.Activo.Value;
        tipo.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Results.Ok(tipo.ToDto());
    }
}
