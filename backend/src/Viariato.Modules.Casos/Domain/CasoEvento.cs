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
    PrioridadCambiada,
    /// <summary>No longer written (how far a robot is lives on the step, not in the history); kept so rows already saved still read.</summary>
    ProgresoActualizado,
    /// <summary>A robot said something happened that may deserve a notice (<c>{ evento, datos }</c>), e.g. "precio_bajado".</summary>
    AvisoDelRobot,
    /// <summary>A notification went out (<c>{ regla, destinatarios }</c>).</summary>
    NotificacionEnviada,
    /// <summary>A notification could not be sent after every attempt (<c>{ regla, destinatarios, error }</c>).</summary>
    NotificacionFallida,
    /// <summary>A reprocessing rule will run a step again (<c>{ regla, numero, limite, paso, programadoPara }</c>).</summary>
    ReprocesamientoProgramado,
    /// <summary>A reprocessing rule ran out of attempts and left the case on its final state (<c>{ regla, limite }</c>).</summary>
    ReprocesamientosAgotados,
    /// <summary>A scheduled reprocessing could not run (<c>{ regla, paso, error }</c>).</summary>
    ReprocesamientoFallido,
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
