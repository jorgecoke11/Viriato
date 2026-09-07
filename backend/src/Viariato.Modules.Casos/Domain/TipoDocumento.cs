namespace Viariato.Modules.Casos.Domain;

/// <summary>Catalog entry a DocumentoClasificacion points at (e.g. "DNI", "Nómina", "Contrato").
/// Global, not scoped to a Flujo — managed by its own admin CRUD.</summary>
public sealed class TipoDocumento
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
