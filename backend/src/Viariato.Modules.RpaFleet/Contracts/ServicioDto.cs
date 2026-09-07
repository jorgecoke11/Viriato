namespace Viariato.Modules.RpaFleet.Contracts;

public sealed record ServicioDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    bool Activo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateServicioRequest(string Nombre, string? Descripcion);

public sealed record UpdateServicioRequest(string? Nombre, string? Descripcion, bool? Activo);
