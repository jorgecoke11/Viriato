namespace Viariato.Modules.Flujos.Contracts;

public sealed record AgenteDefinicionDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    string Modelo,
    string SystemPrompt,
    string? HerramientasPermitidas,
    string? ParametrosJson,
    bool Activo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateAgenteDefinicionRequest(
    string Nombre,
    string? Descripcion,
    string Modelo,
    string SystemPrompt,
    string? HerramientasPermitidas,
    string? ParametrosJson);

public sealed record UpdateAgenteDefinicionRequest(
    string? Nombre,
    string? Descripcion,
    string? Modelo,
    string? SystemPrompt,
    string? HerramientasPermitidas,
    string? ParametrosJson,
    bool? Activo);
