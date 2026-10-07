using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Contracts;

public sealed record FlujoParametroDto(
    Guid Id,
    Guid FlujoId,
    string Codigo,
    string Valor,
    string? Descripcion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CreateFlujoParametroRequest(string Codigo, string Valor, string? Descripcion);

/// <summary>The code is the key a robot asks by, so it is not editable. Both fields are always replaced:
/// a null <c>Descripcion</c> clears it.</summary>
public sealed record UpdateFlujoParametroRequest(string Valor, string? Descripcion);

public static class FlujoParametroDtoMapper
{
    public static FlujoParametroDto ToDto(this FlujoParametro parametro) => new(
        parametro.Id, parametro.FlujoId, parametro.Codigo, parametro.Valor, parametro.Descripcion, parametro.CreatedAt, parametro.UpdatedAt);
}
