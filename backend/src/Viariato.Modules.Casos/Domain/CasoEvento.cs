namespace Viariato.Modules.Casos.Domain;

public enum CasoEventoAccion
{
    Creado,
    Iniciado,
    DatosActualizados,
    PasoIniciado,
    PasoCompletado,
    PasoFallido,
    PasoReintentado,
    Pausado,
    Reanudado,
    RevisionSolicitada,
    RevisionResuelta,
    Completado,
    Cancelado,
    Fallido,
    EstadoNegocioActualizado,
}

/// <summary>Flat, append-only audit trail — same shape and spirit as Users' RoleAuditLog. Every state
/// transition the orchestrator makes writes one of these inline, never as a hidden side effect, so a
/// Caso's full history can always be reconstructed and future real-time push has a clean hook.</summary>
public sealed class CasoEvento
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Null means the engine itself caused this transition, not a person.</summary>
    public Guid? ActorUserId { get; set; }

    public Guid CasoId { get; set; }

    public Guid? EjecucionId { get; set; }

    public CasoEventoAccion Accion { get; set; }

    /// <summary>jsonb, optional context (previous/new estado, a DatosJson snapshot, etc.).</summary>
    public string? DetalleJson { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
