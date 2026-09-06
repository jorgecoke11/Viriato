namespace Viariato.Modules.Flujos.Contracts;

public sealed record FlujoTipoCasoDefDto(
    Guid Id,
    Guid FlujoId,
    string Nombre,
    int Orden,
    bool Activo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateFlujoTipoCasoRequest(string Nombre, int Orden);

public sealed record UpdateFlujoTipoCasoRequest(string? Nombre, int? Orden, bool? Activo);
