namespace Viariato.Modules.Casos.Domain;

/// <summary>One page range within a Documento classified as a given TipoDocumento — a single
/// multi-page Documento can carry several of these (e.g. pages 1-2 "DNI", pages 3-4 "Nómina").</summary>
public sealed class DocumentoClasificacion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid DocumentoId { get; set; }

    public Guid TipoDocumentoId { get; set; }

    public TipoDocumento? TipoDocumento { get; set; }

    public int PaginaDesde { get; set; }

    public int PaginaHasta { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }
}
