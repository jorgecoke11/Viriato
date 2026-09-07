using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Contracts;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Validation;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>
/// A Documento's page-range classifications. Hand-written (not the generic Crud kit) because every
/// operation must re-derive the owning Caso's Flujo to enforce the same AsignacionFlujo boundary the
/// rest of the Documento endpoints already do.
/// </summary>
internal static class DocumentoClasificacionesEndpoints
{
    public static void MapDocumentoClasificacionesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/documentos/{documentoId:guid}/clasificaciones").RequireAuthorization();

        group.MapGet("/", ListAsync).RequireAuthorization(Permissions.CasosRead);
        group.MapPost("/", CreateAsync).RequireAuthorization(Permissions.CasosManage);
        group.MapPatch("/{id:guid}", UpdateAsync).RequireAuthorization(Permissions.CasosManage);
        group.MapDelete("/{id:guid}", DeleteAsync).RequireAuthorization(Permissions.CasosManage);
    }

    private static async Task<Documento?> LoadDocumentoConAccesoAsync(AppDbContext db, Guid documentoId, Guid userId, CancellationToken ct)
    {
        var documento = await db.Set<Documento>().FirstOrDefaultAsync(d => d.Id == documentoId, ct);
        if (documento is null) return null;

        var caso = await db.Set<Caso>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == documento.CasoId, ct);
        if (caso is null) return null;

        return await FlujoAccessAuthorization.TieneAccesoAlFlujoAsync(db, userId, caso.FlujoId, ct) ? documento : null;
    }

    private static async Task<IResult> ListAsync(Guid documentoId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var documento = await LoadDocumentoConAccesoAsync(db, documentoId, http.User.GetUserId(), ct);
        if (documento is null) return ProblemResults.NotFound(http, "Documento no encontrado.");

        var clasificaciones = await db.Set<DocumentoClasificacion>().AsNoTracking()
            .Include(c => c.TipoDocumento)
            .Where(c => c.DocumentoId == documentoId)
            .OrderBy(c => c.PaginaDesde)
            .ToListAsync(ct);

        return Results.Ok(clasificaciones.Select(c => c.ToDto()).ToList());
    }

    private static async Task<IResult> CreateAsync(
        Guid documentoId,
        CreateDocumentoClasificacionRequest request,
        CreateDocumentoClasificacionRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var documento = await LoadDocumentoConAccesoAsync(db, documentoId, http.User.GetUserId(), ct);
        if (documento is null) return ProblemResults.NotFound(http, "Documento no encontrado.");

        var tipo = await db.Set<TipoDocumento>().FirstOrDefaultAsync(t => t.Id == request.TipoDocumentoId, ct);
        if (tipo is null) return ProblemResults.NotFound(http, "Tipo de documento no encontrado.");

        var clasificacion = new DocumentoClasificacion
        {
            DocumentoId = documentoId,
            TipoDocumentoId = tipo.Id,
            PaginaDesde = request.PaginaDesde,
            PaginaHasta = request.PaginaHasta,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedByUserId = http.User.GetUserId(),
        };
        db.Add(clasificacion);
        await db.SaveChangesAsync(ct);

        clasificacion.TipoDocumento = tipo;
        return Results.Ok(clasificacion.ToDto());
    }

    private static async Task<IResult> UpdateAsync(
        Guid documentoId,
        Guid id,
        UpdateDocumentoClasificacionRequest request,
        UpdateDocumentoClasificacionRequestValidator validator,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var documento = await LoadDocumentoConAccesoAsync(db, documentoId, http.User.GetUserId(), ct);
        if (documento is null) return ProblemResults.NotFound(http, "Documento no encontrado.");

        var clasificacion = await db.Set<DocumentoClasificacion>().Include(c => c.TipoDocumento)
            .FirstOrDefaultAsync(c => c.Id == id && c.DocumentoId == documentoId, ct);
        if (clasificacion is null) return ProblemResults.NotFound(http, "Clasificación no encontrada.");

        if (request.TipoDocumentoId is not null)
        {
            var tipo = await db.Set<TipoDocumento>().FirstOrDefaultAsync(t => t.Id == request.TipoDocumentoId, ct);
            if (tipo is null) return ProblemResults.NotFound(http, "Tipo de documento no encontrado.");
            clasificacion.TipoDocumentoId = tipo.Id;
            clasificacion.TipoDocumento = tipo;
        }

        if (request.PaginaDesde is not null) clasificacion.PaginaDesde = request.PaginaDesde.Value;
        if (request.PaginaHasta is not null) clasificacion.PaginaHasta = request.PaginaHasta.Value;

        await db.SaveChangesAsync(ct);

        return Results.Ok(clasificacion.ToDto());
    }

    private static async Task<IResult> DeleteAsync(Guid documentoId, Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var documento = await LoadDocumentoConAccesoAsync(db, documentoId, http.User.GetUserId(), ct);
        if (documento is null) return ProblemResults.NotFound(http, "Documento no encontrado.");

        var clasificacion = await db.Set<DocumentoClasificacion>().FirstOrDefaultAsync(c => c.Id == id && c.DocumentoId == documentoId, ct);
        if (clasificacion is null) return ProblemResults.NotFound(http, "Clasificación no encontrada.");

        db.Remove(clasificacion);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }
}
