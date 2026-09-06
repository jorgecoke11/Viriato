namespace Viariato.Modules.Flujos.Contracts;

public sealed record FlujoEstadoDefDto(
    Guid Id,
    Guid FlujoId,
    string Codigo,
    string Display,
    int Orden,
    bool EsFinal,
    bool Activo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateFlujoEstadoDefRequest(string Codigo, string Display, int Orden, bool EsFinal);

public sealed record UpdateFlujoEstadoDefRequest(string? Display, int? Orden, bool? EsFinal, bool? Activo);
