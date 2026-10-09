using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Contracts;
using Viariato.Modules.Casos.Orchestration;
using Viariato.Modules.Casos.Validation;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Flujos.Esquemas;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>
/// Creating a Caso the easy way: the user picks a creator the process's author configured and gives its data (and, if they
/// want, a title of their own). Everything else comes from the creator. It goes through exactly the same checks as creating a
/// Caso by hand — the assignment to the Flujo included — it just asks for much less.
/// </summary>
internal static class CreadoresDeCasoUsoEndpoints
{
    public static void MapCreadoresDeCasoUsoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/creadores-de-caso").RequireAuthorization(Permissions.CasosCrear);
        group.MapGet("/", ListDisponiblesAsync);
        group.MapPost("/{id:guid}/casos", CrearCasoAsync);
    }

    /// <summary>The creators this user can use: active ones, of processes they are assigned to and that have a published version.</summary>
    private static async Task<IResult> ListDisponiblesAsync(AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var asignados = await FlujoAccessAuthorization.FlujosAsignadosAsync(db, http.User.GetUserId(), ct);

        var creadores = await db.Set<CreadorDeCaso>().AsNoTracking()
            .Include(c => c.Flujo).Include(c => c.TipoCaso)
            .Where(c => c.Activo && asignados.Contains(c.FlujoId) && c.Flujo!.Activo && c.Flujo.VersionActivaId != null)
            .OrderBy(c => c.Flujo!.Nombre).ThenBy(c => c.Orden).ThenBy(c => c.Nombre)
            .ToListAsync(ct);

        var ahora = DateTimeOffset.Now;
        return Results.Ok(creadores
            .Select(c => new CreadorDisponibleDto(
                c.Id, c.Nombre, c.Descripcion, c.FlujoId, c.Flujo!.Nombre, c.TipoCasoId, c.TipoCaso?.Nombre, c.TipoCaso?.EsquemaDatosJson,
                TituloAutogenerado(c, c.Secuencia + 1, ahora, datosJson: null)))
            .ToList());
    }

    private static async Task<IResult> CrearCasoAsync(
        Guid id,
        CrearCasoDesdeCreadorRequest request,
        StartCasoRequestValidator validator,
        AppDbContext db,
        IEjecucionOrchestrator orchestrator,
        HttpContext http,
        CancellationToken ct)
    {
        var creador = await db.Set<CreadorDeCaso>().Include(c => c.Flujo).Include(c => c.TipoCaso)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        // Same answer for "does not exist", "retired" and "not yours": an unassigned user must not learn which exist.
        if (creador is null || !creador.Activo || creador.Flujo is not { Activo: true } ||
            !await FlujoAccessAuthorization.TieneAccesoAlFlujoAsync(db, http.User.GetUserId(), creador.FlujoId, ct))
        {
            return ProblemResults.NotFound(http, "Creador no encontrado.");
        }

        if (creador.Flujo.VersionActivaId is not { } versionId) return ProblemResults.Conflict(http, "El proceso no tiene una versión activa.");

        Guid? pasoInicialId = null;
        if (creador.PasoInicialNombre is { } nombrePaso)
        {
            pasoInicialId = await db.Set<FlujoPasoDef>().AsNoTracking()
                .Where(p => p.FlujoVersionId == versionId && p.Nombre == nombrePaso)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);
            if (pasoInicialId is null)
            {
                return ProblemResults.Conflict(http, $"La versión activa del proceso ya no tiene el paso «{nombrePaso}»: revisa este creador.");
            }
        }

        // The number is taken from the database in one statement, so two people creating at the same moment never share one.
        // A case that is then rejected leaves a gap in the numbering, which is better than a repeated number.
        var titulo = request.Titulo?.Trim();
        if (string.IsNullOrEmpty(titulo))
        {
            // `update … returning` cannot be composed over, so the row is read back as a list.
            var n = (await db.Database
                .SqlQuery<long>($"update flujos.creadores_de_caso set secuencia = secuencia + 1 where id = {creador.Id} returning secuencia as \"Value\"")
                .ToListAsync(ct)).Single();
            titulo = TituloAutogenerado(creador, n, DateTimeOffset.Now, request.DatosJson);
        }

        var inicio = new StartCasoRequest(
            creador.FlujoId, null, titulo, string.IsNullOrWhiteSpace(request.DatosJson) ? null : request.DatosJson,
            creador.EstadoNegocioInicialId, creador.TipoCasoId, pasoInicialId);

        var validation = validator.Validate(inicio);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        return await CasosEndpoints.IniciarCasoAsync(inicio, db, orchestrator, http, ct);
    }

    private static string TituloAutogenerado(CreadorDeCaso creador, long n, DateTimeOffset ahora, string? datosJson) =>
        PlantillaDeTitulo.Renderizar(
            creador.PlantillaTitulo,
            new Dictionary<string, string>
            {
                ["creador"] = creador.Nombre,
                ["proceso"] = creador.Flujo?.Nombre ?? string.Empty,
                ["tipo"] = creador.TipoCaso?.Nombre ?? string.Empty,
                ["fecha"] = ahora.ToString("yyyy-MM-dd"),
                ["hora"] = ahora.ToString("HH:mm"),
                ["n"] = n.ToString(),
            },
            datosJson);
}
