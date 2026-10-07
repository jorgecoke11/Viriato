using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.RpaFleet.Contracts;
using Viariato.Modules.RpaFleet.Domain;
using Viariato.Modules.RpaFleet.Security;
using Viariato.Modules.RpaFleet.Validation;
using Viariato.Shared;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.RpaFleet.Endpoints;

/// <summary>
/// The admin side of credentials. Hand-written, not the generic Crud kit: the password is write-only (it is
/// encrypted on the way in and no response here ever carries it), and updating means "keep the stored
/// password unless a new one is sent" — which the kit's uniform ToDto/ApplyUpdate shape cannot express.
/// </summary>
internal static class CredencialesEndpoints
{
    public static void MapCredencialesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/credenciales").RequireAuthorization(Permissions.RpaManage);

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
    }

    private static async Task<IResult> ListAsync(string? searchTerm, int? page, int? pageSize, AppDbContext db, CancellationToken ct)
    {
        var currentPage = page is null or < 1 ? 1 : page.Value;
        var currentPageSize = pageSize is null or < 1 or > 100 ? 20 : pageSize.Value;

        var query = db.Set<Credencial>().AsNoTracking().Include(c => c.Servicio).AsQueryable();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(c => c.Nombre.ToLower().Contains(term) || (c.Usuario != null && c.Usuario.ToLower().Contains(term)));
        }

        var ordered = query.OrderBy(c => c.Nombre);
        var total = await ordered.CountAsync(ct);
        var items = await ordered.Skip((currentPage - 1) * currentPageSize).Take(currentPageSize).ToListAsync(ct);

        return Results.Ok(new PagedResult<CredencialDto>(items.Select(c => c.ToDto()).ToList(), currentPage, currentPageSize, total));
    }

    private static async Task<IResult> CreateAsync(
        CreateCredencialRequest request,
        CreateCredencialRequestValidator validator,
        ICredencialProtector protector,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        if (request.ServicioId is { } servicioId && !await db.Set<Servicio>().AnyAsync(s => s.Id == servicioId, ct))
        {
            return ProblemResults.NotFound(http, "Servicio no encontrado.");
        }

        if (await db.Set<Credencial>().AnyAsync(c => c.Nombre == request.Nombre, ct))
        {
            return ProblemResults.Conflict(http, "Ya existe una credencial con ese nombre.");
        }

        var now = DateTimeOffset.UtcNow;
        var credencial = new Credencial
        {
            Nombre = request.Nombre,
            Descripcion = NullIfEmpty(request.Descripcion),
            Usuario = NullIfEmpty(request.Usuario),
            PasswordCifrado = string.Empty,
            ServicioId = request.ServicioId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        // The id is bound into the ciphertext, so it has to exist before encrypting.
        credencial.PasswordCifrado = protector.Proteger(request.Password, credencial.Id);

        db.Add(credencial);
        await db.SaveChangesAsync(ct);

        var creada = await db.Set<Credencial>().AsNoTracking().Include(c => c.Servicio).FirstAsync(c => c.Id == credencial.Id, ct);
        return Results.Ok(creada.ToDto());
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateCredencialRequest request,
        UpdateCredencialRequestValidator validator,
        ICredencialProtector protector,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var credencial = await db.Set<Credencial>().Include(c => c.Servicio).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (credencial is null) return ProblemResults.NotFound(http, "Credencial no encontrada.");

        if (request.ServicioId is { } servicioId && !await db.Set<Servicio>().AnyAsync(s => s.Id == servicioId, ct))
        {
            return ProblemResults.NotFound(http, "Servicio no encontrado.");
        }

        credencial.Descripcion = NullIfEmpty(request.Descripcion);
        credencial.Usuario = NullIfEmpty(request.Usuario);
        credencial.ServicioId = request.ServicioId;
        credencial.Activo = request.Activo;
        if (!string.IsNullOrEmpty(request.Password))
        {
            credencial.PasswordCifrado = protector.Proteger(request.Password, credencial.Id);
        }

        credencial.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        // Reload so the Servicio name in the response matches the (possibly changed) ServicioId.
        var actualizada = await db.Set<Credencial>().AsNoTracking().Include(c => c.Servicio).FirstAsync(c => c.Id == id, ct);
        return Results.Ok(actualizada.ToDto());
    }

    private static async Task<IResult> DeleteAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var credencial = await db.Set<Credencial>().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (credencial is null) return ProblemResults.NotFound(http, "Credencial no encontrada.");

        db.Remove(credencial);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
