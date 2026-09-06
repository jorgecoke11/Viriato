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
/// Business-facing states the user defines per Flujo — entirely separate from the engine's own
/// CasoEstado lifecycle. Codigo is never editable once created (steps reference it externally), and
/// there is no delete: a state that's no longer wanted is retired via Activo=false, since a script
/// out there may still report it and a Caso may already point at it.
/// </summary>
internal static class FlujoEstadosEndpoints
{
    public static void MapFlujoEstadosEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var read = endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/estados").RequireAuthorization(Permissions.FlujosRead);
        read.MapGet("/", ListEstadosAsync);

        var manage = endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/estados").RequireAuthorization(Permissions.FlujosManage);
        manage.MapPost("/", CreateEstadoAsync);
        manage.MapPatch("/{id:guid}", UpdateEstadoAsync);
    }

    private static async Task<IResult> ListEstadosAsync(Guid flujoId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var flujoExiste = await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct);
        if (!flujoExiste) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        var estados = await db.Set<FlujoEstadoDef>().AsNoTracking()
            .Where(e => e.FlujoId == flujoId)
            .OrderBy(e => e.Orden)
            .Select(e => e.ToDto())
            .ToListAsync(ct);

        return Results.Ok(estados);
    }

    private static async Task<IResult> CreateEstadoAsync(
        Guid flujoId,
        CreateFlujoEstadoDefRequest request,
        CreateFlujoEstadoDefRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var flujoExiste = await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct);
        if (!flujoExiste) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        var codigoTomado = await db.Set<FlujoEstadoDef>().AnyAsync(e => e.FlujoId == flujoId && e.Codigo == request.Codigo, ct);
        if (codigoTomado) return ProblemResults.Conflict(http, "Ya existe un estado con ese código en este flujo.");

        var now = DateTimeOffset.UtcNow;
        var estado = new FlujoEstadoDef
        {
            FlujoId = flujoId,
            Codigo = request.Codigo,
            Display = request.Display,
            Orden = request.Orden,
            EsFinal = request.EsFinal,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Add(estado);
        await db.SaveChangesAsync(ct);

        return Results.Ok(estado.ToDto());
    }

    private static async Task<IResult> UpdateEstadoAsync(
        Guid flujoId,
        Guid id,
        UpdateFlujoEstadoDefRequest request,
        UpdateFlujoEstadoDefRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var estado = await db.Set<FlujoEstadoDef>().FirstOrDefaultAsync(e => e.Id == id && e.FlujoId == flujoId, ct);
        if (estado is null) return ProblemResults.NotFound(http, "Estado no encontrado.");

        if (request.Display is not null) estado.Display = request.Display;
        if (request.Orden is not null) estado.Orden = request.Orden.Value;
        if (request.EsFinal is not null) estado.EsFinal = request.EsFinal.Value;
        if (request.Activo is not null) estado.Activo = request.Activo.Value;
        estado.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Results.Ok(estado.ToDto());
    }
}
