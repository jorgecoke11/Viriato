namespace Viariato.Modules.Casos.Contracts;

public sealed record TipoDocumentoDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    bool Activo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateTipoDocumentoRequest(string Nombre, string? Descripcion);

public sealed record UpdateTipoDocumentoRequest(string? Nombre, string? Descripcion, bool? Activo);
