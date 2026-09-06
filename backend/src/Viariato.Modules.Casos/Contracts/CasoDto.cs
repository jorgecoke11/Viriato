namespace Viariato.Modules.Casos.Contracts;

/// <summary>The user-configured business status shown to end users — see FlujoEstadoDef. Entirely
/// separate from the technical `Estado` (CasoEstado) alongside it in the DTOs below.</summary>
public sealed record EstadoNegocioDto(string Codigo, string Display);

public sealed record CasoListItemDto(
    Guid Id,
    string Titulo,
    string Estado,
    EstadoNegocioDto? EstadoNegocio,
    string? TipoCaso,
    Guid FlujoId,
    Guid FlujoVersionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

public sealed record CasoDetailDto(
    Guid Id,
    string Titulo,
    string Estado,
    EstadoNegocioDto? EstadoNegocio,
    string? TipoCaso,
    Guid FlujoId,
    Guid FlujoVersionId,
    string? DatosJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    EjecucionDto? EjecucionActual,
    IReadOnlyList<CasoEventoDto> EventosRecientes);

public sealed record StartCasoRequest(Guid? FlujoId, Guid? FlujoVersionId, string Titulo, string? DatosJson, Guid? EstadoNegocioInicialId, Guid? TipoCasoId);

public sealed record UpdateCasoDatosRequest(string DatosJson);
