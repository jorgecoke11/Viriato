namespace Viariato.Modules.Casos.Domain;

/// <summary>One full run of a <see cref="Caso"/> through its bound FlujoVersion.</summary>
public sealed class Ejecucion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CasoId { get; set; }

    public Caso? Caso { get; set; }

    /// <summary>Denormalized copy of Caso.FlujoVersionId.</summary>
    public Guid FlujoVersionId { get; set; }

    public EjecucionEstado Estado { get; set; } = EjecucionEstado.Pendiente;

    /// <summary>
    /// Explicit pointer to the in-flight attempt (not just the step definition), so pausing and
    /// resuming re-enters exactly the same attempt instead of guessing which NumeroIntento was active.
    /// </summary>
    public Guid? PasoActualId { get; set; }

    public EjecucionPaso? PasoActual { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public ICollection<EjecucionPaso> Pasos { get; set; } = new List<EjecucionPaso>();
}
