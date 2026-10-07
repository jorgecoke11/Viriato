namespace Viariato.Modules.Flujos.Contracts;

public sealed record FlujoTipoCasoDefDto(
    Guid Id,
    Guid FlujoId,
    string Nombre,
    int Orden,
    bool Activo,
    string? EsquemaDatosJson,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateFlujoTipoCasoRequest(string Nombre, int Orden, string? EsquemaDatosJson = null);

/// <param name="EsquemaDatosJson">Sets the form of the type's business data (null leaves it as it is).</param>
/// <param name="QuitarEsquema">True removes the form: the data goes back to free-form JSON.</param>
public sealed record UpdateFlujoTipoCasoRequest(
    string? Nombre, int? Orden, bool? Activo, string? EsquemaDatosJson = null, bool QuitarEsquema = false);
