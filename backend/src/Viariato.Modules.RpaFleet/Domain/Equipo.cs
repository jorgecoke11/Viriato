namespace Viariato.Modules.RpaFleet.Domain;

/// <summary>A physical machine or VM a robot can run on. Purely a catalog entry — identity/liveness
/// of the actual process is proven by the Despliegue's API key, not by anything stored here.</summary>
public sealed class Equipo
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>How many steps the robots of this machine may be running at the same time. 1 (the default) is the safe
    /// setting for robots that drive the screen or share a browser: they take turns instead of fighting over it.</summary>
    public int MaxEjecucionesSimultaneas { get; set; } = 1;

    /// <summary>How the machine chooses among services that all have work waiting; see <see cref="EquipoServicioOrden"/>.</summary>
    public PoliticaDespacho Politica { get; set; } = PoliticaDespacho.Prioridad;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
