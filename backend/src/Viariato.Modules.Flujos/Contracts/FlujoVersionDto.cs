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
    IReadOnlyList<FlujoPasoDefDto> Pasos,
    IReadOnlyList<FlujoTransicionDefDto> Transiciones);

/// <param name="PasoOrigenId">Null for the start arrow.</param>
/// <param name="Resultado">"Completada" or "Fallida"; null on the start arrow.</param>
/// <param name="EstadoNegocioCodigo">Null for "any state".</param>
public sealed record FlujoTransicionDefDto(
    Guid Id,
    Guid? PasoOrigenId,
    string? Resultado,
    Guid? EstadoNegocioId,
    string? EstadoNegocioCodigo,
    Guid PasoDestinoId);

public sealed record CreateFlujoVersionRequest(string? Notas);

/// <param name="Clave">The client's own name for the step within this request, which <see cref="FlujoTransicionInput"/> refers to (new
/// steps have no Id yet). Required when the request carries arrows.</param>
public sealed record FlujoPasoDefInput(
    int Orden,
    string Nombre,
    string TipoPaso,
    Guid? AgenteDefinicionId,
    Guid? ServicioId,
    string? ConfiguracionJson,
    string? Clave = null,
    double? PosicionX = null,
    double? PosicionY = null,
    int? MaximoDeVisitas = null);

/// <param name="Origen">The <see cref="FlujoPasoDefInput.Clave"/> of the step the arrow leaves, or null for the start arrow.</param>
/// <param name="Destino">The <see cref="FlujoPasoDefInput.Clave"/> of the step it goes to.</param>
/// <param name="Resultado">"Completada" or "Fallida"; must be null on the start arrow.</param>
/// <param name="EstadoNegocioCodigo">The business state the Caso must be in, or null for any. A code the process does not have yet
/// is created when the version is saved.</param>
public sealed record FlujoTransicionInput(string? Origen, string Destino, string? Resultado = null, string? EstadoNegocioCodigo = null);

/// <param name="Transiciones">The flow's arrows. Null keeps the older, list-shaped flow (steps run by Orden, with Decision steps).
/// With arrows, steps are nodes: a Decision step is not allowed, each servicio appears once, and a Rpa step's configuration is dropped.</param>
public sealed record ReplacePasosRequest(IReadOnlyList<FlujoPasoDefInput> Pasos, IReadOnlyList<FlujoTransicionInput>? Transiciones = null);
