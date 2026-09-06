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

internal static class AgentesEndpoints
{
    public static void MapAgentesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapReadOnlyCrud("/api/v1/agentes", new ReadOnlyCrudResource<AgenteDefinicion, AgenteDefinicionDto>
        {
            Id = a => a.Id,
            ToDto = a => a.ToDto(),
            Search = (query, term) => query.Where(a => a.Nombre.ToLower().Contains(term.ToLower())),
            Authorize = (http, ct) => FlujosAuthorization.RequireClaimAsync(http, Permissions.FlujosRead, ct),
        });

        var manage = endpoints.MapGroup("/api/v1/agentes").RequireAuthorization(Permissions.FlujosManage);

        manage.MapPost("/", CreateAgenteAsync);
        manage.MapPatch("/{id:guid}", UpdateAgenteAsync);
        manage.MapDelete("/{id:guid}", DeleteAgenteAsync);
    }

    private static async Task<IResult> CreateAgenteAsync(
        CreateAgenteDefinicionRequest request,
        CreateAgenteDefinicionRequestValidator validator,
        AppDbContext db,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var now = DateTimeOffset.UtcNow;
        var agente = new AgenteDefinicion
        {
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            Modelo = request.Modelo,
            SystemPrompt = request.SystemPrompt,
            HerramientasPermitidas = request.HerramientasPermitidas,
            ParametrosJson = request.ParametrosJson,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.Add(agente);
        await db.SaveChangesAsync(ct);

        return Results.Ok(agente.ToDto());
    }

    private static async Task<IResult> UpdateAgenteAsync(
        Guid id,
        UpdateAgenteDefinicionRequest request,
        UpdateAgenteDefinicionRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var agente = await db.Set<AgenteDefinicion>().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (agente is null) return ProblemResults.NotFound(http, "Agente no encontrado.");

        if (request.Nombre is not null) agente.Nombre = request.Nombre;
        if (request.Descripcion is not null) agente.Descripcion = request.Descripcion;
        if (request.Modelo is not null) agente.Modelo = request.Modelo;
        if (request.SystemPrompt is not null) agente.SystemPrompt = request.SystemPrompt;
        if (request.HerramientasPermitidas is not null) agente.HerramientasPermitidas = request.HerramientasPermitidas;
        if (request.ParametrosJson is not null) agente.ParametrosJson = request.ParametrosJson;
        if (request.Activo is not null) agente.Activo = request.Activo.Value;
        agente.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Results.Ok(agente.ToDto());
    }

    private static async Task<IResult> DeleteAgenteAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var agente = await db.Set<AgenteDefinicion>().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (agente is null) return ProblemResults.NotFound(http, "Agente no encontrado.");

        var enUso = await db.Set<FlujoPasoDef>().AnyAsync(p => p.AgenteDefinicionId == id, ct);
        if (enUso) return ProblemResults.Conflict(http, "No se puede eliminar un agente referenciado por pasos de flujo.");

        db.Remove(agente);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }
}
