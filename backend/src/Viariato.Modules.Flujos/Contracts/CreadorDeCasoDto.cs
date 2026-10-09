namespace Viariato.Modules.Flujos.Contracts;

public sealed record CreadorDeCasoDto(
    Guid Id,
    Guid FlujoId,
    string Nombre,
    string? Descripcion,
    Guid? TipoCasoId,
    string? TipoCasoNombre,
    string? PasoInicialNombre,
    Guid? EstadoNegocioInicialId,
    string? EstadoNegocioInicialDisplay,
    string PlantillaTitulo,
    int Orden,
    bool Activo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>The whole creator, for both creating and replacing one: the optional references are cleared by sending them empty,
/// which a partial update cannot tell apart from "leave it".</summary>
/// <param name="PlantillaTitulo">Empty: the default template.</param>
public sealed record GuardarCreadorDeCasoRequest(
    string Nombre,
    string? Descripcion,
    Guid? TipoCasoId,
    string? PasoInicialNombre,
    Guid? EstadoNegocioInicialId,
    string? PlantillaTitulo,
    int Orden,
    bool Activo = true);
