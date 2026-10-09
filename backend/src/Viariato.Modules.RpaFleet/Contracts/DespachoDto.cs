namespace Viariato.Modules.RpaFleet.Contracts;

public sealed record ServicioOrdenDto(Guid ServicioId, string ServicioNombre);

/// <summary>A service the machine actually runs (it has at least one Despliegue there), so the screen can offer
/// exactly those to put in order.</summary>
public sealed record ServicioDesplegadoDto(Guid ServicioId, string ServicioNombre, int Despliegues);

/// <summary>How one machine dispatches: how many steps at once, how it chooses between services, and the order.</summary>
public sealed record DespachoEquipoDto(
    Guid EquipoId,
    string EquipoNombre,
    int MaxEjecucionesSimultaneas,
    string Politica,
    IReadOnlyList<ServicioOrdenDto> Orden,
    IReadOnlyList<ServicioDesplegadoDto> ServiciosDelEquipo);

/// <param name="Politica"><c>Prioridad</c> or <c>Turnos</c>.</param>
/// <param name="Orden">Service ids, first to last. Services the machine runs that are left out are served after these.</param>
public sealed record UpdateDespachoEquipoRequest(int MaxEjecucionesSimultaneas, string Politica, IReadOnlyList<Guid> Orden);

public sealed record PlantillaDespachoDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    int MaxEjecucionesSimultaneas,
    string Politica,
    IReadOnlyList<ServicioOrdenDto> Orden,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SavePlantillaDespachoRequest(
    string Nombre, string? Descripcion, int MaxEjecucionesSimultaneas, string Politica, IReadOnlyList<Guid> Orden);
