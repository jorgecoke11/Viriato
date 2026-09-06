namespace Viariato.Modules.Flujos.Domain;

public enum FlujoVersionEstado
{
    Borrador,
    Publicada,
    Archivada,
}

/// <summary>An immutable-once-published snapshot of a <see cref="Flujo"/>'s steps. A <see cref="Caso"/>
/// binds permanently to one of these — never to the mutable <see cref="Flujo"/> itself — so editing a
/// Flujo later can never change how an already-running or already-finished Caso was processed.</summary>
public sealed class FlujoVersion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FlujoId { get; set; }

    public Flujo? Flujo { get; set; }

    /// <summary>1, 2, 3… per Flujo.</summary>
    public int NumeroVersion { get; set; }

    public FlujoVersionEstado Estado { get; set; } = FlujoVersionEstado.Borrador;

    public string? Notas { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public ICollection<FlujoPasoDef> Pasos { get; set; } = new List<FlujoPasoDef>();
}
