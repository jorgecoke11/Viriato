using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Contracts;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Storage;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>
/// Records a piece of proof produced while a step ran. In a real deployment this is called by the
/// executor itself (e.g. an RPA robot posting its screenshot); exposed as its own endpoint so it's
/// not tied to any single executor's implementation.
/// </summary>
internal static class EvidenciasEndpoints
{
    public static void MapEvidenciasEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/casos/{casoId:guid}/pasos/{ejecucionPasoId:guid}/evidencias", CrearEvidenciaAsync)
            .RequireAuthorization(Permissions.CasosManage)
            .DisableAntiforgery();
    }

    private static async Task<IResult> CrearEvidenciaAsync(
        Guid casoId,
        Guid ejecucionPasoId,
        [FromForm] string tipo,
        [FromForm] string titulo,
        [FromForm] string? contenidoJson,
        IFormFile? file,
        AppDbContext db,
        IDocumentStorage storage,
        HttpContext http,
        CancellationToken ct)
    {
        var caso = await db.Set<Caso>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == casoId, ct);
        if (caso is null) return ProblemResults.NotFound(http, "Caso no encontrado.");
        if (!await FlujoAccessAuthorization.TieneAccesoAlFlujoAsync(db, http.User.GetUserId(), caso.FlujoId, ct))
        {
            return ProblemResults.NotFound(http, "Caso no encontrado.");
        }

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
            var storageKey = await storage.SaveAsync(stream, file.FileName, ct);

            var documento = new Documento
            {
                CasoId = casoId,
                EjecucionPasoId = ejecucionPasoId,
                Nombre = file.FileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                TamanoBytes = file.Length,
                StorageKey = storageKey,
                UploadedByUserId = http.User.GetUserId(),
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
