using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Flujos.Contracts;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Flujos.Esquemas;
using Viariato.Modules.Flujos.Validation;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Flujos.Endpoints;

/// <summary>
/// The forms of a process: a library its creators pick from. Whoever configures the creators manages them. A form a creator uses cannot
/// be deleted (the creator would silently go back to free-form JSON); one that only Casos remember can — they keep their data.
/// </summary>
internal static class FormulariosEndpoints
{
    public static void MapFormulariosEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/flujos/{flujoId:guid}/formularios", ListAsync).RequireAuthorization(Permissions.FlujosRead);

        var manage = endpoints.MapGroup("/api/v1/flujos/{flujoId:guid}/formularios").RequireAuthorization(Permissions.FlujosCreadores);
        manage.MapPost("/", CreateAsync);
        manage.MapPut("/{id:guid}", ReplaceAsync);
        manage.MapDelete("/{id:guid}", DeleteAsync);
    }

    private static async Task<IResult> ListAsync(Guid flujoId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (!await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct)) return ProblemResults.NotFound(http, "Flujo no encontrado.");

        var formularios = await db.Set<Formulario>().AsNoTracking()
            .Where(f => f.FlujoId == flujoId)
            .OrderBy(f => f.Nombre)
            .ToListAsync(ct);
        var usos = await UsosAsync(db, flujoId, ct);

        return Results.Ok(formularios.Select(f => f.ToDto(usos.GetValueOrDefault(f.Id, []))).ToList());
    }

    private static async Task<IResult> CreateAsync(
        Guid flujoId, GuardarFormularioRequest request, GuardarFormularioRequestValidator validator,
        AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        if (!await db.Set<Flujo>().AnyAsync(f => f.Id == flujoId, ct)) return ProblemResults.NotFound(http, "Flujo no encontrado.");
        var nombre = request.Nombre.Trim();
        if (await db.Set<Formulario>().AnyAsync(f => f.FlujoId == flujoId && f.Nombre == nombre, ct))
        {
            return ProblemResults.Conflict(http, "Ya existe un formulario con ese nombre en este proceso.");
        }

        var now = DateTimeOffset.UtcNow;
        var formulario = new Formulario { FlujoId = flujoId, Nombre = nombre, EsquemaJson = request.EsquemaJson, CreatedAt = now, UpdatedAt = now };
        Aplicar(formulario, request);

        db.Add(formulario);
        await db.SaveChangesAsync(ct);

        return Results.Ok(formulario.ToDto([]));
    }

    private static async Task<IResult> ReplaceAsync(
        Guid flujoId, Guid id, GuardarFormularioRequest request, GuardarFormularioRequestValidator validator,
        AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var formulario = await db.Set<Formulario>().FirstOrDefaultAsync(f => f.Id == id && f.FlujoId == flujoId, ct);
        if (formulario is null) return ProblemResults.NotFound(http, "Formulario no encontrado.");

        var nombre = request.Nombre.Trim();
        if (await db.Set<Formulario>().AnyAsync(f => f.FlujoId == flujoId && f.Nombre == nombre && f.Id != id, ct))
        {
            return ProblemResults.Conflict(http, "Ya existe un formulario con ese nombre en este proceso.");
        }

        // The defaults of the creators that use it were written for the form as it was: a change that leaves one of them invalid would
        // only surface later, in a launcher at night or in front of a person who cannot fix it.
        var creadores = await db.Set<CreadorDeCaso>().AsNoTracking()
            .Where(c => c.FormularioId == id && c.DatosPorDefectoJson != null)
            .Select(c => new { c.Nombre, c.DatosPorDefectoJson })
            .ToListAsync(ct);
        foreach (var creador in creadores)
        {
            var errores = EsquemaDatos.ValidarDatos(request.EsquemaJson, creador.DatosPorDefectoJson, parcial: true);
            if (errores.Count > 0)
            {
                return ProblemResults.Conflict(http,
                    $"Los valores por defecto del creador «{creador.Nombre}» dejarían de encajar con el formulario: {string.Join(" ", errores)} Cámbialos antes.");
            }
        }

        formulario.Nombre = nombre;
        Aplicar(formulario, request);
        formulario.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var usos = await UsosAsync(db, flujoId, ct);
        return Results.Ok(formulario.ToDto(usos.GetValueOrDefault(formulario.Id, [])));
    }

    private static async Task<IResult> DeleteAsync(Guid flujoId, Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var formulario = await db.Set<Formulario>().FirstOrDefaultAsync(f => f.Id == id && f.FlujoId == flujoId, ct);
        if (formulario is null) return ProblemResults.NotFound(http, "Formulario no encontrado.");

        var usadoPor = await db.Set<CreadorDeCaso>().Where(c => c.FormularioId == id).OrderBy(c => c.Nombre).Select(c => c.Nombre).ToListAsync(ct);
        if (usadoPor.Count > 0)
        {
            return ProblemResults.Conflict(http,
                $"Lo usa{(usadoPor.Count > 1 ? "n los creadores" : " el creador")} {string.Join(", ", usadoPor.Select(n => $"«{n}»"))}: cámbiales antes el formulario.");
        }

        db.Remove(formulario);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static void Aplicar(Formulario formulario, GuardarFormularioRequest request)
    {
        formulario.Descripcion = string.IsNullOrWhiteSpace(request.Descripcion) ? null : request.Descripcion.Trim();
        formulario.EsquemaJson = request.EsquemaJson;
    }

    private static async Task<Dictionary<Guid, List<string>>> UsosAsync(AppDbContext db, Guid flujoId, CancellationToken ct) =>
        (await db.Set<CreadorDeCaso>().AsNoTracking()
            .Where(c => c.FlujoId == flujoId && c.FormularioId != null)
            .OrderBy(c => c.Orden).ThenBy(c => c.Nombre)
            .Select(c => new { FormularioId = c.FormularioId!.Value, c.Nombre })
            .ToListAsync(ct))
        .GroupBy(c => c.FormularioId)
        .ToDictionary(g => g.Key, g => g.Select(c => c.Nombre).ToList());
}
