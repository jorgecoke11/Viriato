namespace Viariato.Modules.Casos.Contracts;

/// <summary>The user-configured business status shown to end users — see FlujoEstadoDef. Entirely
/// separate from the technical `Estado` (CasoEstado) alongside it in the DTOs below.</summary>
/// <param name="EsFinal">Whether the Caso is over once it reaches this estado (the process says so), as opposed to one it passes through.</param>
public sealed record EstadoNegocioDto(string Codigo, string Display, bool EsFinal = false);

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
    DateTimeOffset? CompletedAt,
    int? ProgresoPorcentaje = null,
    bool EnVivo = false);

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

public sealed record StartCasoRequest(
    Guid? FlujoId, Guid? FlujoVersionId, string Titulo, string? DatosJson, Guid? EstadoNegocioInicialId, Guid? TipoCasoId, Guid? PasoInicialId);

/// <param name="Titulo">The title the user chose, or empty to let the creator write it.</param>
public sealed record CrearCasoDesdeCreadorRequest(string? Titulo, string? DatosJson);

/// <summary>A creator as a user sees it when picking one. <paramref name="TituloEjemplo"/> is what the title would be if they
/// created a case right now, to show as the placeholder of the title field.</summary>
public sealed record CreadorDisponibleDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    Guid FlujoId,
    string FlujoNombre,
    Guid? TipoCasoId,
    string? TipoCasoNombre,
    string? EsquemaDatosJson,
    string TituloEjemplo);

public sealed record UpdateCasoDatosRequest(string DatosJson);

/// <param name="Ids">The Casos the action is for. At most <see cref="Validation.AccionMasivaRequestValidator.MaximoPorPeticion"/> at once.</param>
/// <param name="Parametros">What the action needs besides the selection (the priority to give, say); each action says what it
/// reads, and most need nothing.</param>
public sealed record AccionMasivaRequest(IReadOnlyList<Guid> Ids, System.Text.Json.JsonElement? Parametros = null);

/// <param name="Motivo">Why that item was left as it was (it did not apply, it does not exist or is not visible to the caller…).</param>
public sealed record ItemOmitidoDto(Guid Id, string Motivo);

/// <summary>What a bulk action did, whatever the action: how many it was applied to and, for each one it was not, why not.</summary>
public sealed record ResultadoAccionMasiva(int Procesados, IReadOnlyList<ItemOmitidoDto> Omitidos);

/// <param name="Prioridad">Higher goes first among the executions waiting for the same service; 0 is the ordinary one.</param>
public sealed record CambiarPrioridadRequest(int Prioridad);
