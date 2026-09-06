using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Casos.Domain;

/// <summary>
/// The unit of business work flowing through the platform (e.g. one expediente, one alta de cliente).
/// Bound permanently to the exact <c>FlujoVersion</c> it started with — never to the mutable Flujo —
/// so a later edit to the Flujo can never change how this Caso was, or is being, processed.
/// Never hard-deleted: it moves to <see cref="CasoEstado.Cancelado"/> instead, which keeps the
/// Caso&lt;-&gt;Ejecucion cross-reference (see <see cref="EjecucionActualId"/>) simple to maintain.
/// </summary>
public sealed class Caso
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Denormalized copy of the Flujo id for query convenience (also derivable via FlujoVersion).</summary>
    public Guid FlujoId { get; set; }

    public Guid FlujoVersionId { get; set; }

    public required string Titulo { get; set; }

    public CasoEstado Estado { get; set; } = CasoEstado.Iniciado;

    /// <summary>
    /// The user-configured business status (see <see cref="FlujoEstadoDef"/>) — purely informational,
    /// entirely separate from <see cref="Estado"/>. Null until a step reports one via its
    /// ConfiguracionJson's "estadoNegocioCodigo"; the engine's own lifecycle never depends on this.
    /// </summary>
    public Guid? EstadoNegocioActualId { get; set; }

    public FlujoEstadoDef? EstadoNegocioActual { get; set; }

    /// <summary>
    /// The category chosen when this Caso was created (see <see cref="FlujoTipoCasoDef"/>) —
    /// orthogonal to <see cref="EstadoNegocioActual"/>: it groups Casos regardless of where each one
    /// currently stands, and never changes on its own.
    /// </summary>
    public Guid? TipoCasoId { get; set; }

    public FlujoTipoCasoDef? TipoCaso { get; set; }

    /// <summary>jsonb, current business data. Evolves per Flujo — no giant fixed schema.</summary>
    public string? DatosJson { get; set; }

    /// <summary>Set once the first <see cref="Ejecucion"/> is created (Caso is inserted first, so this starts null).</summary>
    public Guid? EjecucionActualId { get; set; }

    public Ejecucion? EjecucionActual { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }
}
