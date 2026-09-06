using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Casos.Domain;

/// <summary>
/// One concrete attempt at running a FlujoPasoDef within an Ejecucion. A retry never mutates a failed
/// row — it inserts a new one with <see cref="NumeroIntento"/> + 1, so "an attempt" stays a single
/// uniform concept that documents, evidencias, and the agent/RPA detail tables can all key off
/// directly, without a separate attempts sub-table duplicating those relationships one level down.
/// </summary>
public sealed class EjecucionPaso
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EjecucionId { get; set; }

    public Ejecucion? Ejecucion { get; set; }

    /// <summary>Denormalized for direct "all steps of this caso" queries.</summary>
    public Guid CasoId { get; set; }

    public Guid FlujoPasoDefId { get; set; }

    public FlujoPasoDef? FlujoPasoDef { get; set; }

    /// <summary>Copied from FlujoPasoDef at creation time — safe, since a published FlujoVersion is immutable.</summary>
    public TipoPaso TipoPaso { get; set; }

    public int NumeroIntento { get; set; } = 1;

    public EjecucionPasoEstado Estado { get; set; } = EjecucionPasoEstado.Pendiente;

    /// <summary>Set for step types executed via ITrabajoTracker (typically Rpa/Agente/Api). Null for
    /// Interno/Decision steps that run synchronously inline in the orchestrator.</summary>
    public Guid? TrabajoId { get; set; }

    public string? ErrorMensaje { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
