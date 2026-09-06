namespace Viariato.Modules.Casos.Contracts;

public sealed record EvidenciaDto(
    Guid Id,
    Guid EjecucionPasoId,
    Guid CasoId,
    string Tipo,
    string Titulo,
    string? ContenidoJson,
    Guid? DocumentoId,
    DateTimeOffset CreatedAt);
