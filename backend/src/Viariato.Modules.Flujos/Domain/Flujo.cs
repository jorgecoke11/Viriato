namespace Viariato.Modules.Flujos.Domain;

/// <summary>Stable identity for a reusable process (e.g. "Alta de cliente"). Never runs by itself —
/// a <see cref="Caso"/> always runs a specific, immutable <see cref="FlujoVersion"/> of it.</summary>
public sealed class Flujo
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>The one <see cref="FlujoVersion"/> new Casos start on. Null until first published.</summary>
    public Guid? VersionActivaId { get; set; }

    public FlujoVersion? VersionActiva { get; set; }

    /// <summary>Soft toggle to hide a Flujo from "start a new Caso" pickers without deleting it.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Where this Flujo's evidencias/documentos are written. Null until an admin assigns one.</summary>
    public Guid? StorageConfigId { get; set; }

    public StorageConfig? StorageConfig { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public ICollection<FlujoVersion> Versiones { get; set; } = new List<FlujoVersion>();
}
