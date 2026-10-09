using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.RpaFleet.Contracts;
using Viariato.Modules.RpaFleet.Domain;
using Viariato.Modules.RpaFleet.Validation;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.RpaFleet.Endpoints;

/// <summary>
/// How each machine decides which of its services goes first: the order and policy per machine, and the templates
/// that can be applied to machines. The decision itself is taken where steps are handed out (see the Casos module's
/// worker endpoints) — this is only the configuration it reads.
/// </summary>
internal static class DespachoEndpoints
{
    public static void MapDespachoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var equipo = endpoints.MapGroup("/api/v1/equipos/{equipoId:guid}/despacho").RequireAuthorization(Permissions.RpaManage);
        equipo.MapGet("/", GetDespachoAsync);
        equipo.MapPut("/", UpdateDespachoAsync);
        equipo.MapPost("/plantilla/{plantillaId:guid}", AplicarPlantillaAsync);

        var plantillas = endpoints.MapGroup("/api/v1/plantillas-despacho").RequireAuthorization(Permissions.RpaManage);
        plantillas.MapGet("/", ListPlantillasAsync);
        plantillas.MapPost("/", CreatePlantillaAsync);
        plantillas.MapPut("/{id:guid}", UpdatePlantillaAsync);
        plantillas.MapDelete("/{id:guid}", DeletePlantillaAsync);
    }

    // ---------------------------------------------------------------- per machine

    private static async Task<IResult> GetDespachoAsync(Guid equipoId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var dto = await CargarDespachoAsync(db, equipoId, ct);
        return dto is null ? ProblemResults.NotFound(http, "Equipo no encontrado.") : Results.Ok(dto);
    }

    private static async Task<IResult> UpdateDespachoAsync(
        Guid equipoId,
        UpdateDespachoEquipoRequest request,
        IValidator<UpdateDespachoEquipoRequest> validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var equipo = await db.Set<Equipo>().FirstOrDefaultAsync(e => e.Id == equipoId, ct);
        if (equipo is null) return ProblemResults.NotFound(http, "Equipo no encontrado.");

        if (await ServiciosInexistentesAsync(db, request.Orden, ct) is { } faltan)
        {
            return ProblemResults.Conflict(http, faltan);
        }

        await AplicarAsync(db, equipo, request.MaxEjecucionesSimultaneas, DespachoReglas.LeerPolitica(request.Politica), request.Orden, ct);
        return Results.Ok(await CargarDespachoAsync(db, equipoId, ct));
    }

    private static async Task<IResult> AplicarPlantillaAsync(Guid equipoId, Guid plantillaId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var equipo = await db.Set<Equipo>().FirstOrDefaultAsync(e => e.Id == equipoId, ct);
        if (equipo is null) return ProblemResults.NotFound(http, "Equipo no encontrado.");

        var plantilla = await db.Set<PlantillaDespacho>().AsNoTracking().Include(p => p.Servicios).FirstOrDefaultAsync(p => p.Id == plantillaId, ct);
        if (plantilla is null) return ProblemResults.NotFound(http, "Plantilla no encontrada.");

        // A copy, on purpose: the machine ends up with its own order, free to differ from the template afterwards.
        await AplicarAsync(
            db, equipo, plantilla.MaxEjecucionesSimultaneas, plantilla.Politica,
            plantilla.Servicios.OrderBy(s => s.Orden).Select(s => s.ServicioId).ToList(), ct);
        return Results.Ok(await CargarDespachoAsync(db, equipoId, ct));
    }

    private static async Task AplicarAsync(
        AppDbContext db, Equipo equipo, int max, PoliticaDespacho politica, IReadOnlyList<Guid> orden, CancellationToken ct)
    {
        equipo.MaxEjecucionesSimultaneas = max;
        equipo.Politica = politica;
        equipo.UpdatedAt = DateTimeOffset.UtcNow;

        var actuales = await db.Set<EquipoServicioOrden>().Where(o => o.EquipoId == equipo.Id).ToListAsync(ct);
        db.RemoveRange(actuales);
        for (var i = 0; i < orden.Count; i++)
        {
            db.Add(new EquipoServicioOrden { EquipoId = equipo.Id, ServicioId = orden[i], Orden = i });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task<DespachoEquipoDto?> CargarDespachoAsync(AppDbContext db, Guid equipoId, CancellationToken ct)
    {
        var equipo = await db.Set<Equipo>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == equipoId, ct);
        if (equipo is null) return null;

        var orden = await db.Set<EquipoServicioOrden>().AsNoTracking()
            .Where(o => o.EquipoId == equipoId).OrderBy(o => o.Orden)
            .Select(o => new ServicioOrdenDto(o.ServicioId, o.Servicio!.Nombre))
            .ToListAsync(ct);

        var desplegados = await db.Set<Despliegue>().AsNoTracking()
            .Where(d => d.EquipoId == equipoId)
            .GroupBy(d => new { d.ServicioId, d.Servicio!.Nombre })
            .Select(g => new ServicioDesplegadoDto(g.Key.ServicioId, g.Key.Nombre, g.Count()))
            .ToListAsync(ct);

        return new DespachoEquipoDto(
            equipo.Id, equipo.Nombre, equipo.MaxEjecucionesSimultaneas, equipo.Politica.ToString(), orden,
            desplegados.OrderBy(s => s.ServicioNombre).ToList());
    }

    // ---------------------------------------------------------------- templates

    private static async Task<IResult> ListPlantillasAsync(AppDbContext db, CancellationToken ct)
    {
        var plantillas = await db.Set<PlantillaDespacho>().AsNoTracking()
            .Include(p => p.Servicios).ThenInclude(s => s.Servicio)
            .OrderBy(p => p.Nombre).ToListAsync(ct);
        return Results.Ok(plantillas.Select(ToDto).ToList());
    }

    private static async Task<IResult> CreatePlantillaAsync(
        SavePlantillaDespachoRequest request,
        IValidator<SavePlantillaDespachoRequest> validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var nombre = request.Nombre.Trim();
        if (await db.Set<PlantillaDespacho>().AnyAsync(p => p.Nombre == nombre, ct)) return ProblemResults.Conflict(http, "Ya existe una plantilla con ese nombre.");
        if (await ServiciosInexistentesAsync(db, request.Orden, ct) is { } faltan) return ProblemResults.Conflict(http, faltan);

        var now = DateTimeOffset.UtcNow;
        var plantilla = new PlantillaDespacho
        {
            Nombre = nombre,
            Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim(),
            MaxEjecucionesSimultaneas = request.MaxEjecucionesSimultaneas,
            Politica = DespachoReglas.LeerPolitica(request.Politica),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Add(plantilla);
        db.AddRange(request.Orden.Select((servicioId, i) => new PlantillaDespachoServicio { PlantillaId = plantilla.Id, ServicioId = servicioId, Orden = i }));
        await db.SaveChangesAsync(ct);

        return Results.Ok(await CargarPlantillaAsync(db, plantilla.Id, ct));
    }

    private static async Task<IResult> UpdatePlantillaAsync(
        Guid id,
        SavePlantillaDespachoRequest request,
        IValidator<SavePlantillaDespachoRequest> validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var plantilla = await db.Set<PlantillaDespacho>().Include(p => p.Servicios).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plantilla is null) return ProblemResults.NotFound(http, "Plantilla no encontrada.");

        var nombre = request.Nombre.Trim();
        if (await db.Set<PlantillaDespacho>().AnyAsync(p => p.Id != id && p.Nombre == nombre, ct)) return ProblemResults.Conflict(http, "Ya existe una plantilla con ese nombre.");
        if (await ServiciosInexistentesAsync(db, request.Orden, ct) is { } faltan) return ProblemResults.Conflict(http, faltan);

        plantilla.Nombre = nombre;
        plantilla.Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
        plantilla.MaxEjecucionesSimultaneas = request.MaxEjecucionesSimultaneas;
        plantilla.Politica = DespachoReglas.LeerPolitica(request.Politica);
        plantilla.UpdatedAt = DateTimeOffset.UtcNow;

        db.RemoveRange(plantilla.Servicios);
        plantilla.Servicios.Clear();
        db.AddRange(request.Orden.Select((servicioId, i) => new PlantillaDespachoServicio { PlantillaId = plantilla.Id, ServicioId = servicioId, Orden = i }));
        await db.SaveChangesAsync(ct);

        return Results.Ok(await CargarPlantillaAsync(db, id, ct));
    }

    private static async Task<IResult> DeletePlantillaAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var plantilla = await db.Set<PlantillaDespacho>().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (plantilla is null) return ProblemResults.NotFound(http, "Plantilla no encontrada.");

        // Machines the template was applied to keep their own copy; nothing points back at it.
        db.Remove(plantilla);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<PlantillaDespachoDto> CargarPlantillaAsync(AppDbContext db, Guid id, CancellationToken ct) =>
        ToDto(await db.Set<PlantillaDespacho>().AsNoTracking().Include(p => p.Servicios).ThenInclude(s => s.Servicio).FirstAsync(p => p.Id == id, ct));

    private static PlantillaDespachoDto ToDto(PlantillaDespacho p) => new(
        p.Id, p.Nombre, p.Descripcion, p.MaxEjecucionesSimultaneas, p.Politica.ToString(),
        p.Servicios.OrderBy(s => s.Orden).Select(s => new ServicioOrdenDto(s.ServicioId, s.Servicio?.Nombre ?? string.Empty)).ToList(),
        p.CreatedAt, p.UpdatedAt);

    private static async Task<string?> ServiciosInexistentesAsync(AppDbContext db, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return null;

        var existentes = await db.Set<Servicio>().Where(s => ids.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct);
        return existentes.Count == ids.Distinct().Count() ? null : "Uno o más servicios del orden no existen.";
    }
}
