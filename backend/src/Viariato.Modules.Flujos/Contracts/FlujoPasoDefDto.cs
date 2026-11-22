namespace Viariato.Modules.Flujos.Contracts;

public sealed record FlujoPasoDefDto(
    Guid Id,
    Guid FlujoVersionId,
    int Orden,
    string Nombre,
    string TipoPaso,
    Guid? AgenteDefinicionId,
    Guid? ServicioId,
    string? ConfiguracionJson,
    double? PosicionX = null,
    double? PosicionY = null,
    int? MaximoDeVisitas = null);
