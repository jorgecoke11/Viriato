namespace Viariato.Modules.RpaFleet.Domain;

/// <summary>One entry of a machine's execution order: where a service stands in the order the machine serves its
/// services in (0 is first). A service the machine runs but that is not listed is served after all the listed
/// ones. Rows are plain configuration — removing the machine or the service removes them.</summary>
public sealed class EquipoServicioOrden
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EquipoId { get; set; }

    public Equipo? Equipo { get; set; }

    public Guid ServicioId { get; set; }

    public Servicio? Servicio { get; set; }

    public int Orden { get; set; }
}
