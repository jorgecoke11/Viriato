using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Contracts;

public sealed record FlujoParametroDto(
    Guid Id,
    Guid FlujoId,
    string Codigo,
    string Valor,
    string? Descripcion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool EditablePorUsuario = false,
    string? Etiqueta = null);

public sealed record CreateFlujoParametroRequest(
    string Codigo, string Valor, string? Descripcion, bool EditablePorUsuario = false, string? Etiqueta = null);

/// <summary>The code is the key a robot asks by, so it is not editable. The value and the description are always replaced: a null
/// <c>Descripcion</c> clears it. The two dashboard fields are left as they are when not sent (null), so a client that does not know
/// about them cannot switch them off by accident; an empty <c>Etiqueta</c> clears it.</summary>
public sealed record UpdateFlujoParametroRequest(string Valor, string? Descripcion, bool? EditablePorUsuario = null, string? Etiqueta = null);

/// <summary>A parameter as the person working the process sees it: only the ones opened up to them, under a name they understand.</summary>
public sealed record ParametroEditableDto(Guid Id, string Codigo, string Etiqueta, string? Descripcion, string Valor, DateTimeOffset UpdatedAt);

public sealed record ValorDeParametroRequest(Guid Id, string Valor);

/// <summary>Several values at once: the form on the dashboard saves them together or not at all.</summary>
public sealed record GuardarParametrosEditablesRequest(IReadOnlyList<ValorDeParametroRequest> Valores);

public static class FlujoParametroDtoMapper
{
    public static FlujoParametroDto ToDto(this FlujoParametro parametro) => new(
        parametro.Id, parametro.FlujoId, parametro.Codigo, parametro.Valor, parametro.Descripcion, parametro.CreatedAt, parametro.UpdatedAt,
        parametro.EditablePorUsuario, parametro.Etiqueta);

    public static ParametroEditableDto ToEditableDto(this FlujoParametro parametro) => new(
        parametro.Id, parametro.Codigo, parametro.Etiqueta ?? parametro.Codigo, parametro.Descripcion, parametro.Valor, parametro.UpdatedAt);
}
