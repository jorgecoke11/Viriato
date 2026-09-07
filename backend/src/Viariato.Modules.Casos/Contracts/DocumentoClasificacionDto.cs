namespace Viariato.Modules.Casos.Contracts;

public sealed record DocumentoClasificacionDto(
    Guid Id,
    Guid DocumentoId,
    Guid TipoDocumentoId,
    string TipoDocumentoNombre,
    int PaginaDesde,
    int PaginaHasta,
    DateTimeOffset CreatedAt);

public sealed record CreateDocumentoClasificacionRequest(Guid TipoDocumentoId, int PaginaDesde, int PaginaHasta);

public sealed record UpdateDocumentoClasificacionRequest(Guid? TipoDocumentoId, int? PaginaDesde, int? PaginaHasta);
