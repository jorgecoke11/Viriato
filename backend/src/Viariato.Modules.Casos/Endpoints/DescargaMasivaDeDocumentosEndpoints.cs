using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Contracts;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Storage;
using Viariato.Modules.Casos.Validation;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>
/// All the documents of a selection of Casos in a single zip, one folder per case. It is the bulk action "download documents": a
/// download rather than a change, so it does not go through the generic bulk endpoint (whose answer is a report) but follows its
/// rules — a case that does not exist or is in a process the caller is not assigned to is reported as not found, one with no
/// documents is left out and said so, and at most <see cref="AccionMasivaRequestValidator.MaximoPorPeticion"/> per request.
/// What goes in is what the Documentos tab of each case lists (screenshots and videos stay out: they are looked at in the
/// timeline). The report travels in the <c>X-Viriato-Resumen</c> header, and also inside the zip as <c>LEEME.txt</c>.
/// </summary>
internal static class DescargaMasivaDeDocumentosEndpoints
{
    /// <summary>More than this in one zip is a job for several downloads, not for one request that ties up the disk and the connection.</summary>
    public const long MaximoDeBytes = 2L * 1024 * 1024 * 1024;

    public const int MaximoDeDocumentos = 5000;

    /// <summary>How many left-out cases the header names; the rest are only counted there (and all are in the zip's LEEME).</summary>
    private const int OmitidosEnLaCabecera = 50;

    public const string CabeceraDeResumen = "X-Viriato-Resumen";

    public static void MapDescargaMasivaDeDocumentosEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/casos/documentos/zip", DescargarAsync).RequireAuthorization(Permissions.CasosDescargar);
    }

    private static async Task<IResult> DescargarAsync(
        AccionMasivaRequest request,
        AccionMasivaRequestValidator validator,
        AppDbContext db,
        IDocumentStorageResolver storageResolver,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid) return ProblemResults.ValidationProblem(validation);

        var ids = request.Ids.Distinct().ToList();
        var asignados = await FlujoAccessAuthorization.FlujosAsignadosAsync(db, http.User.GetUserId(), ct);
        var casos = (await db.Set<Caso>().AsNoTracking()
                .Where(c => ids.Contains(c.Id))
                .Select(c => new { c.Id, c.Titulo, c.FlujoId })
                .ToListAsync(ct))
            .Where(c => asignados.Contains(c.FlujoId))
            .OrderBy(c => c.Titulo, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var omitidos = ids.Where(id => casos.All(c => c.Id != id)).Select(id => new ItemOmitidoDto(id, "Caso no encontrado.")).ToList();

        var visibles = casos.Select(c => c.Id).ToList();
        var documentos = (await DocumentosDelExpediente.DeLosCasos(db, visibles).AsNoTracking()
                .OrderBy(d => d.CreatedAt)
                .ToListAsync(ct))
            .ToLookup(d => d.CasoId);

        var total = documentos.Sum(g => g.Count());
        var bytes = documentos.SelectMany(g => g).Sum(d => d.TamanoBytes);
        if (total > MaximoDeDocumentos || bytes > MaximoDeBytes)
        {
            return Results.Problem(
                title: "Demasiado para un solo zip",
                detail: $"Los casos elegidos tienen {total} documentos ({FormatoDeTamano(bytes)}). Un zip admite hasta {MaximoDeDocumentos} documentos y " +
                        $"{FormatoDeTamano(MaximoDeBytes)}: elige menos casos.",
                statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        omitidos.AddRange(casos.Where(c => !documentos[c.Id].Any()).Select(c => new ItemOmitidoDto(c.Id, "No tiene documentos.")));

        if (total == 0)
        {
            PonerResumen(http, 0, 0, omitidos);
            return Results.NoContent();
        }

        // Built on disk, not in memory (a zip of many files can be big) and not straight onto the response (a zip is written
        // synchronously). The file removes itself when the response has finished with it.
        var temporal = new FileStream(
            Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        var noLeidos = new List<string>();
        var casosConDocumentos = 0;
        var incluidos = 0;

        try
        {
            using (var zip = new ZipArchive(temporal, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var porProceso in casos.Where(c => documentos[c.Id].Any()).GroupBy(c => c.FlujoId))
                {
                    var storage = await storageResolver.ResolveForFlujoAsync(porProceso.Key, ct);
                    try
                    {
                        foreach (var caso in porProceso)
                        {
                            var carpeta = NombresParaZip.CarpetaDeCaso(caso.Titulo, caso.Id);
                            var usados = NombresParaZip.ConjuntoDeNombres();
                            var enEsteCaso = 0;

                            foreach (var documento in documentos[caso.Id])
                            {
                                Stream contenido;
                                try
                                {
                                    contenido = await storage.OpenReadAsync(documento.StorageKey, ct);
                                }
                                catch (Exception ex) when (ex is not OperationCanceledException)
                                {
                                    // A file that is gone from storage must not spoil the zip of everything else: it is said in LEEME.
                                    noLeidos.Add($"{carpeta}/{documento.Nombre}");
                                    continue;
                                }

                                await using (contenido)
                                {
                                    var entrada = zip.CreateEntry($"{carpeta}/{NombresParaZip.Unico(usados, NombresParaZip.Sanear(documento.Nombre))}", CompressionLevel.Fastest);
                                    entrada.LastWriteTime = documento.CreatedAt;
                                    await using var destino = entrada.Open();
                                    await contenido.CopyToAsync(destino, ct);
                                }

                                enEsteCaso++;
                            }

                            if (enEsteCaso > 0) casosConDocumentos++;
                            else omitidos.Add(new ItemOmitidoDto(caso.Id, "No se pudo leer ninguno de sus documentos."));
                            incluidos += enEsteCaso;
                        }
                    }
                    finally
                    {
                        await storage.DisposeAsync();
                    }
                }

                var leeme = zip.CreateEntry("LEEME.txt", CompressionLevel.Optimal);
                await using var escritor = new StreamWriter(leeme.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                await escritor.WriteAsync(Leeme(incluidos, casosConDocumentos, omitidos, noLeidos));
            }
        }
        catch
        {
            await temporal.DisposeAsync();
            throw;
        }

        temporal.Position = 0;
        PonerResumen(http, incluidos, casosConDocumentos, omitidos);
        return Results.File(temporal, "application/zip", $"documentos-{DateTime.Now:yyyyMMdd-HHmm}.zip");
    }

    private static void PonerResumen(HttpContext http, int documentos, int casosConDocumentos, IReadOnlyList<ItemOmitidoDto> omitidos)
    {
        var resumen = JsonSerializer.Serialize(
            new { documentos, casosConDocumentos, omitidosTotal = omitidos.Count, omitidos = omitidos.Take(OmitidosEnLaCabecera) },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        // Base64 so that whatever a message says (accents, quotes) travels safely in a header.
        http.Response.Headers[CabeceraDeResumen] = Convert.ToBase64String(Encoding.UTF8.GetBytes(resumen));
    }

    private static string Leeme(int documentos, int casos, IReadOnlyList<ItemOmitidoDto> omitidos, IReadOnlyList<string> noLeidos)
    {
        var texto = new StringBuilder();
        texto.AppendLine($"Documentos descargados de Viriato el {DateTime.Now:dd/MM/yyyy HH:mm}");
        texto.AppendLine();
        texto.AppendLine($"{documentos} documentos de {casos} casos, uno por carpeta.");
        texto.AppendLine("Solo están los documentos del expediente (las capturas y los vídeos se ven en el historial de cada caso).");

        if (omitidos.Count > 0)
        {
            texto.AppendLine().AppendLine("Casos que no están en el zip:");
            foreach (var omitido in omitidos) texto.AppendLine($"  - {omitido.Id:N}: {omitido.Motivo}");
        }

        if (noLeidos.Count > 0)
        {
            texto.AppendLine().AppendLine("Documentos que existen pero no se pudieron leer del almacenamiento:");
            foreach (var ruta in noLeidos) texto.AppendLine($"  - {ruta}");
        }

        return texto.ToString();
    }

    private static string FormatoDeTamano(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.#} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0.#} MB",
        _ => $"{Math.Max(1, bytes / 1024)} KB",
    };
}
