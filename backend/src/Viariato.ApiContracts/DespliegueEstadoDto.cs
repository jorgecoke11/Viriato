namespace Viariato.ApiContracts;

/// <summary>What GET /api/v1/rpa/despliegue reports back — "am I switched on?" plus enough context
/// for a robot's own logs.</summary>
public sealed record DespliegueEstadoDto(bool Encendido, string EquipoNombre, string ServicioNombre, string FlujoNombre);
