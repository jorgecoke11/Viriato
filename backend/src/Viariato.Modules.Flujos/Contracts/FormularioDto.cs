namespace Viariato.Modules.Flujos.Contracts;

/// <param name="Creadores">The names of the creators that use it: what a change to it reaches.</param>
public sealed record FormularioDto(
    Guid Id,
    Guid FlujoId,
    string Nombre,
    string? Descripcion,
    string EsquemaJson,
    IReadOnlyList<string> Creadores,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>The whole form, for both creating and replacing one.</summary>
public sealed record GuardarFormularioRequest(string Nombre, string? Descripcion, string EsquemaJson);
