namespace Viariato.Modules.Casos.Contracts;

public sealed record DocumentoDto(
    Guid Id,
    Guid CasoId,
    Guid? EjecucionPasoId,
    string Nombre,
    string ContentType,
    long TamanoBytes,
    string? Hash,
    DateTimeOffset CreatedAt);
