namespace Viariato.Modules.Casos.Domain;

public enum RevisionDecision
{
    Aprobada,
    Rechazada,
}

/// <summary>1:1 record of how a RevisionHumana step was resolved — who, when, and what they decided.</summary>
public sealed class RevisionHumana
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EjecucionPasoId { get; set; }

    public EjecucionPaso? EjecucionPaso { get; set; }

    public Guid RevisorUserId { get; set; }

    public RevisionDecision Decision { get; set; }

    public string? Comentario { get; set; }

    public DateTimeOffset ResolvedAt { get; set; }
}
