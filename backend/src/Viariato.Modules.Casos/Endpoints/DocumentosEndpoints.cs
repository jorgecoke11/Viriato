using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Contracts;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Storage;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

internal static class DocumentosEndpoints
{
    public static void MapDocumentosEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/casos/{casoId:guid}/documentos", ListDocumentosAsync)
            .RequireAuthorization(Permissions.CasosRead);

        endpoints.MapPost("/api/v1/casos/{casoId:guid}/documentos", UploadDocumentoAsync)
            .RequireAuthorization(Permissions.CasosManage)
            .DisableAntiforgery();

        endpoints.MapGet("/api/v1/documentos/{id:guid}/contenido", DownloadDocumentoAsync)
            .RequireAuthorization(Permissions.CasosRead);
    }

    private static async Task<IResult> ListDocumentosAsync(Guid casoId, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var caso = await db.Set<Caso>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == casoId, ct);
        if (caso is null) return ProblemResults.NotFound(http, "Caso no encontrado.");
        if (!await FlujoAccessAuthorization.TieneAccesoAlFlujoAsync(db, http.User.GetUserId(), caso.FlujoId, ct))
        {
            return ProblemResults.NotFound(http, "Caso no encontrado.");
        }

        var documentos = await db.Set<Documento>().AsNoTracking()
            .Where(d => d.CasoId == casoId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => d.ToDto())
            .ToListAsync(ct);

        return Results.Ok(documentos);
    }

    private static async Task<IResult> UploadDocumentoAsync(
        Guid casoId,
        IFormFile file,
        AppDbContext db,
        IDocumentStorage storage,
        HttpContext http,
        CancellationToken ct)
    {
        var caso = await db.Set<Caso>().FirstOrDefaultAsync(c => c.Id == casoId, ct);
        if (caso is null) return ProblemResults.NotFound(http, "Caso no encontrado.");
        if (!await FlujoAccessAuthorization.TieneAccesoAlFlujoAsync(db, http.User.GetUserId(), caso.FlujoId, ct))
        {
            return ProblemResults.NotFound(http, "Caso no encontrado.");
        }

        if (file.Length == 0) return ProblemResults.Conflict(http, "El archivo está vacío.");

        await using var stream = file.OpenReadStream();
        var storageKey = await storage.SaveAsync(stream, file.FileName, ct);

        var documento = new Documento
        {
            CasoId = casoId,
            Nombre = file.FileName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            TamanoBytes = file.Length,
            StorageKey = storageKey,
            UploadedByUserId = http.User.GetUserId(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Add(documento);
        await db.SaveChangesAsync(ct);

        return Results.Ok(documento.ToDto());
    }

    private static async Task<IResult> DownloadDocumentoAsync(Guid id, AppDbContext db, IDocumentStorage storage, HttpContext http, CancellationToken ct)
    {
        var documento = await db.Set<Documento>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (documento is null) return ProblemResults.NotFound(http, "Documento no encontrado.");

        var caso = await db.Set<Caso>().AsNoTracking().FirstAsync(c => c.Id == documento.CasoId, ct);
        if (!await FlujoAccessAuthorization.TieneAccesoAlFlujoAsync(db, http.User.GetUserId(), caso.FlujoId, ct))
        {
            return ProblemResults.NotFound(http, "Documento no encontrado.");
        }

        var stream = await storage.OpenReadAsync(documento.StorageKey, ct);
        return Results.File(stream, documento.ContentType, documento.Nombre);
    }
}
