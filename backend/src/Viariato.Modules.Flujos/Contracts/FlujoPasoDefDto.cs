namespace Viariato.Modules.Flujos.Contracts;

public sealed record FlujoPasoDefDto(
    Guid Id,
    Guid FlujoVersionId,
    int Orden,
    string Nombre,
    string TipoPaso,
    Guid? AgenteDefinicionId,
    string? ConfiguracionJson);
