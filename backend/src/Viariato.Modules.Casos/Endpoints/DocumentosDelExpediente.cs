using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>
/// What counts as a document of a Caso's expediente, in one place. Every file is stored as a Documento row, but a file that is
/// only <i>visual proof</i> of what a step did — a screenshot, a video — belongs to the timeline, where it is looked at. Any
/// other file a step produces (a CSV, a PDF, a generated report) is a document like one a person uploads: it is listed under
/// Documentos so it can be downloaded, and the timeline keeps its evidence entry as the overview, pointing there.
/// </summary>
internal static class DocumentosDelExpediente
{
    public static IQueryable<Documento> De(AppDbContext db, Guid casoId) =>
        db.Set<Documento>().Where(d => d.CasoId == casoId
            && !db.Set<Evidencia>().Any(e => e.DocumentoId == d.Id && (e.Tipo == EvidenciaTipo.Screenshot || e.Tipo == EvidenciaTipo.Video)));

    /// <summary>The same, for several Casos at once.</summary>
    public static IQueryable<Documento> DeLosCasos(AppDbContext db, IReadOnlyCollection<Guid> casoIds) =>
        db.Set<Documento>().Where(d => casoIds.Contains(d.CasoId)
            && !db.Set<Evidencia>().Any(e => e.DocumentoId == d.Id && (e.Tipo == EvidenciaTipo.Screenshot || e.Tipo == EvidenciaTipo.Video)));
}
