using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Contracts;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Storage;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>
/// The actual "create a Documento (if a file came with it) and an Evidencia row" work shared by both
/// the user-facing evidencias endpoint and the RPA worker one — each does its own access check first
/// (a JWT user's AsignacionFlujo boundary vs. a Despliegue's claimed-step check) since those two
/// identities have nothing in common, then both funnel into this.
/// </summary>
internal static class EvidenciaCreation
{
    public static async Task<IResult> CrearAsync(
        Guid casoId,
        Guid ejecucionPasoId,
        string tipo,
        string titulo,
        string? contenidoJson,
        IFormFile? file,
        Guid flujoId,
        Guid? uploadedByUserId,
        AppDbContext db,
        IDocumentStorageResolver storageResolver,
        HttpContext http,
        CancellationToken ct)
    {
        var pasoExiste = await db.Set<EjecucionPaso>().AnyAsync(p => p.Id == ejecucionPasoId && p.CasoId == casoId, ct);
        if (!pasoExiste) return ProblemResults.NotFound(http, "Paso no encontrado.");

        if (!Enum.TryParse<EvidenciaTipo>(tipo, true, out var tipoParsed))
        {
            return ProblemResults.Conflict(http, "Tipo de evidencia inválido.");
        }

        Guid? documentoId = null;
        if (file is not null && file.Length > 0)
        {
            await using var stream = file.OpenReadStream();
            await using var storage = await storageResolver.ResolveForFlujoAsync(flujoId, ct);
            var storageKey = await storage.SaveAsync(stream, file.FileName, ct);

            var documento = new Documento
            {
                CasoId = casoId,
                EjecucionPasoId = ejecucionPasoId,
                Nombre = file.FileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                TamanoBytes = file.Length,
                StorageKey = storageKey,
                UploadedByUserId = uploadedByUserId,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Add(documento);
            await db.SaveChangesAsync(ct);
            documentoId = documento.Id;
        }

        var evidencia = new Evidencia
        {
            EjecucionPasoId = ejecucionPasoId,
            CasoId = casoId,
            Tipo = tipoParsed,
            Titulo = titulo,
            ContenidoJson = contenidoJson,
            DocumentoId = documentoId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Add(evidencia);
        await db.SaveChangesAsync(ct);

        return Results.Ok(evidencia.ToDto());
    }
}
