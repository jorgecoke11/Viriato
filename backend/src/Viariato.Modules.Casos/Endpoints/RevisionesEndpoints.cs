using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Contracts;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Orchestration;
using Viariato.Modules.Casos.Validation;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>Cross-caso queue of steps waiting on a person — a reviewer works from here rather than
/// having to know which Caso needs attention. Scoped to the caller's AsignacionFlujo, same as every
/// other Casos endpoint.</summary>
internal static class RevisionesEndpoints
{
    public static void MapRevisionesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/revisiones").RequireAuthorization(Permissions.CasosReview);
        group.MapGet("/", ListPendientesAsync);
        group.MapPost("/{ejecucionPasoId:guid}/resolver", ResolverAsync);
    }

    private static async Task<IResult> ListPendientesAsync(AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var flujosAsignados = await FlujoAccessAuthorization.FlujosAsignadosAsync(db, http.User.GetUserId(), ct);

        var pendientes = await (
            from paso in db.Set<EjecucionPaso>().AsNoTracking()
            join caso in db.Set<Caso>().AsNoTracking() on paso.CasoId equals caso.Id
            where paso.Estado == EjecucionPasoEstado.EsperandoRevisionHumana && flujosAsignados.Contains(caso.FlujoId)
            orderby paso.StartedAt
            select new RevisionPendienteDto(paso.Id, caso.Id, caso.Titulo, paso.FlujoPasoDefId, paso.StartedAt)
        ).ToListAsync(ct);

        return Results.Ok(pendientes);
    }

    private static async Task<IResult> ResolverAsync(
        Guid ejecucionPasoId,
        ResolverRevisionRequest request,
        ResolverRevisionRequestValidator validator,
        AppDbContext db,
        IEjecucionOrchestrator orchestrator,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var paso = await db.Set<EjecucionPaso>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == ejecucionPasoId, ct);
        if (paso is null) return ProblemResults.NotFound(http, "Paso no encontrado.");

        var caso = await db.Set<Caso>().AsNoTracking().FirstAsync(c => c.Id == paso.CasoId, ct);
        if (!await FlujoAccessAuthorization.TieneAccesoAlFlujoAsync(db, http.User.GetUserId(), caso.FlujoId, ct))
        {
            return ProblemResults.NotFound(http, "Paso no encontrado.");
        }

        var decision = Enum.Parse<RevisionDecision>(request.Decision, ignoreCase: true);

        try
        {
            await orchestrator.ResolverRevisionAsync(ejecucionPasoId, decision, http.User.GetUserId(), request.Comentario, ct);
            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return ProblemResults.Conflict(http, ex.Message);
        }
    }
}
