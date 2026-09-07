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

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
