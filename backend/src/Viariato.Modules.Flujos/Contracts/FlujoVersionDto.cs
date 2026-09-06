namespace Viariato.Modules.Flujos.Contracts;

public sealed record FlujoVersionDto(
    Guid Id,
    Guid FlujoId,
    int NumeroVersion,
    string Estado,
    string? Notas,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt);

public sealed record FlujoVersionDetailDto(
    Guid Id,
    Guid FlujoId,
    int NumeroVersion,
    string Estado,
    string? Notas,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<FlujoPasoDefDto> Pasos);

public sealed record CreateFlujoVersionRequest(string? Notas);

public sealed record FlujoPasoDefInput(
    int Orden,
    string Nombre,
    string TipoPaso,
    Guid? AgenteDefinicionId,
    string? ConfiguracionJson);

public sealed record ReplacePasosRequest(IReadOnlyList<FlujoPasoDefInput> Pasos);
