namespace Viariato.Modules.RpaFleet.Domain;

/// <summary>A registered deployable unit (an RPA robot, but just as well any other kind of script or
/// worker process) — the identity a FlujoPasoDef.ServicioId points at. Deliberately generic: nothing
/// here assumes it's specifically RPA automation, only that it's something a Despliegue can run.
/// Global catalog, not scoped to a Flujo or Equipo.</summary>
public sealed class Servicio
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>How many steps of this service may be running at once across ALL machines, or null for no limit.
    /// For what is shared beyond one machine — typically an account on a portal that does not allow two sessions —
    /// as opposed to a machine's own limit, which is about that machine's screen and browser.</summary>
    public int? MaxEjecucionesGlobales { get; set; }

    /// <summary>The longest a step of this service may stay in execution, in minutes, or null for no limit. A step
    /// claimed by a robot and still in progress after that long is cut: its Caso is cancelled, so a robot that hangs
    /// (or died without a trace) cannot hold its machine's slot for ever.</summary>
    public int? TiempoMaximoMinutos { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
