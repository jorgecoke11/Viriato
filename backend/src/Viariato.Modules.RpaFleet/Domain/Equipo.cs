namespace Viariato.Modules.RpaFleet.Domain;

/// <summary>A physical machine or VM a robot can run on. Purely a catalog entry — identity/liveness
/// of the actual process is proven by the Despliegue's API key, not by anything stored here.</summary>
public sealed class Equipo
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
