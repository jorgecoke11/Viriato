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

        var documentos = await DocumentosDelExpediente.De(db, casoId).AsNoTracking()
            .Include(d => d.Clasificaciones).ThenInclude(c => c.TipoDocumento)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);

        // A file a robot produced says which step made it.
        var pasoIds = documentos.Where(d => d.EjecucionPasoId is not null).Select(d => d.EjecucionPasoId!.Value).Distinct().ToList();
        var pasos = pasoIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Set<EjecucionPaso>().AsNoTracking()
                .Where(p => pasoIds.Contains(p.Id))
                .Select(p => new { p.Id, Nombre = p.FlujoPasoDef!.Nombre })
                .ToDictionaryAsync(p => p.Id, p => p.Nombre, ct);

        return Results.Ok(documentos
            .Select(d => d.ToDto(d.EjecucionPasoId is { } pasoId && pasos.TryGetValue(pasoId, out var nombre) ? nombre : null))
            .ToList());
    }

    private static async Task<IResult> UploadDocumentoAsync(
        Guid casoId,
        IFormFile file,
        AppDbContext db,
        IDocumentStorageResolver storageResolver,
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
        await using var storage = await storageResolver.ResolveForFlujoAsync(caso.FlujoId, ct);
        var now = DateTimeOffset.UtcNow;
        var storageKey = await storage.SaveAsync(stream, file.FileName, StorageFolders.Documentos(casoId, now), ct);

        var documento = new Documento
        {
            CasoId = casoId,
            Nombre = file.FileName,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            TamanoBytes = file.Length,
            StorageKey = storageKey,
            UploadedByUserId = http.User.GetUserId(),
            CreatedAt = now,
        };

        db.Add(documento);
        await db.SaveChangesAsync(ct);

        return Results.Ok(documento.ToDto());
    }

    private static async Task<IResult> DownloadDocumentoAsync(
        Guid id, AppDbContext db, IDocumentStorageResolver storageResolver, HttpContext http, CancellationToken ct)
    {
        var documento = await db.Set<Documento>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (documento is null) return ProblemResults.NotFound(http, "Documento no encontrado.");

        var caso = await db.Set<Caso>().AsNoTracking().FirstAsync(c => c.Id == documento.CasoId, ct);
        if (!await FlujoAccessAuthorization.TieneAccesoAlFlujoAsync(db, http.User.GetUserId(), caso.FlujoId, ct))
        {
            return ProblemResults.NotFound(http, "Documento no encontrado.");
        }

        // Deliberately not disposed here: for the S3 provider the returned stream reads directly off
        // the storage client's connection, and Results.File only disposes the stream itself once the
        // response finishes — disposing the client now would cut that read short.
        var storage = await storageResolver.ResolveForFlujoAsync(caso.FlujoId, ct);
        var stream = await storage.OpenReadAsync(documento.StorageKey, ct);
        return Results.File(stream, documento.ContentType, documento.Nombre);
    }
}
