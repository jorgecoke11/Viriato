namespace Viariato.Modules.RpaFleet.Contracts;

public sealed record EquipoDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    bool Activo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateEquipoRequest(string Nombre, string? Descripcion);

public sealed record UpdateEquipoRequest(string? Nombre, string? Descripcion, bool? Activo);
