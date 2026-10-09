namespace Viariato.Modules.RpaFleet.Domain;

/// <summary>A physical machine or VM a robot can run on. Purely a catalog entry — identity/liveness
/// of the actual process is proven by the Despliegue's API key, not by anything stored here.</summary>
public sealed class Equipo
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>An optional ceiling on how many steps the robots of this machine may run at the same time. Null (the
    /// default) means none: the capacity is the number of robot instances running, which the stack decides (see
    /// <see cref="InstanciaDeDespliegue"/>). Set it (1 is the safe value for robots that drive the screen or share a
    /// browser, so they take turns) only to hold the machine below what its instances could do.</summary>
    public int? MaxEjecucionesSimultaneas { get; set; }

    /// <summary>How the machine chooses among services that all have work waiting; see <see cref="EquipoServicioOrden"/>.</summary>
    public PoliticaDespacho Politica { get; set; } = PoliticaDespacho.Prioridad;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
