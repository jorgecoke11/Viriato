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
/// The settings of a process that the people working it may change from the dashboard: only the parameters the process's author
/// opened up (<see cref="FlujoParametro.EditablePorUsuario"/>), and only on processes the person is assigned to — a process they
/// cannot see does not exist for them. The author keeps managing every parameter through the ordinary endpoints; this is the
/// narrow door for everyone else, and it can only change a value, never create, rename or delete anything.
/// </summary>
internal static class ParametrosEditablesEndpoints
{
    public static void MapParametrosEditablesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/parametros-editables").RequireAuthorization(Permissions.FlujosParametros);
        group.MapGet("/", ListAsync);
        group.MapPut("/", GuardarAsync);
    }

    private static async Task<IResult> ListAsync(Guid flujoId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (!await EstaAsignadoAsync(db, http, flujoId, ct)) return ProblemResults.NotFound(http, "Proceso no encontrado.");

        var parametros = await db.Set<FlujoParametro>().AsNoTracking()
            .Where(p => p.FlujoId == flujoId && p.EditablePorUsuario)
            .ToListAsync(ct);

        return Results.Ok(Ordenados(parametros));
    }

    /// <summary>All or nothing: if one of the values is for a parameter that does not exist or is not opened up, nothing changes.</summary>
    private static async Task<IResult> GuardarAsync(
        Guid flujoId, GuardarParametrosEditablesRequest request, GuardarParametrosEditablesRequestValidator validator,
        AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);
        if (!await EstaAsignadoAsync(db, http, flujoId, ct)) return ProblemResults.NotFound(http, "Proceso no encontrado.");

        var ids = request.Valores.Select(v => v.Id).ToList();
        var parametros = await db.Set<FlujoParametro>().Where(p => p.FlujoId == flujoId && p.EditablePorUsuario && ids.Contains(p.Id)).ToListAsync(ct);
        if (parametros.Count != ids.Count)
        {
            return ProblemResults.Conflict(http, "Alguno de esos parámetros no existe o ya no se puede cambiar desde el panel. Recarga e inténtalo de nuevo.");
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var parametro in parametros)
        {
            var valor = request.Valores.Single(v => v.Id == parametro.Id).Valor;
            if (valor == parametro.Valor) continue;
            parametro.Valor = valor;
            parametro.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(Ordenados(parametros));
    }

    private static List<ParametroEditableDto> Ordenados(IEnumerable<FlujoParametro> parametros) =>
        parametros.Select(p => p.ToEditableDto()).OrderBy(p => p.Etiqueta, StringComparer.CurrentCultureIgnoreCase).ToList();

    private static Task<bool> EstaAsignadoAsync(AppDbContext db, HttpContext http, Guid flujoId, CancellationToken ct)
    {
        var userId = http.User.GetUserId();
        return db.Set<AsignacionFlujo>().AnyAsync(a => a.UserId == userId && a.FlujoId == flujoId, ct);
    }
}
