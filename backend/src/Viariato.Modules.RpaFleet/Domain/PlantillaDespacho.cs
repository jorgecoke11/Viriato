namespace Viariato.Modules.RpaFleet.Domain;

/// <summary>A saved dispatch setup — how many things a machine runs at once, how it chooses between services, and
/// the order of those services — that can be applied to any number of machines. Applying copies it: the machine
/// then has its own order, which can be adjusted without touching the template (or the other machines).</summary>
public sealed class PlantillaDespacho
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    public int MaxEjecucionesSimultaneas { get; set; } = 1;

    public PoliticaDespacho Politica { get; set; } = PoliticaDespacho.Prioridad;

    public List<PlantillaDespachoServicio> Servicios { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class PlantillaDespachoServicio
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid PlantillaId { get; set; }

    public PlantillaDespacho? Plantilla { get; set; }

    public Guid ServicioId { get; set; }

    public Servicio? Servicio { get; set; }

    public int Orden { get; set; }
}
