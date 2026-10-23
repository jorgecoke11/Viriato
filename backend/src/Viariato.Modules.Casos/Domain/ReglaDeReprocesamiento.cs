namespace Viariato.Modules.Casos.Domain;

/// <summary>
/// A reprocessing rule of a process: when one of its cases ends (completed, failed or cut off for running too long — never when a person
/// discarded it) in <see cref="EstadoNegocioId"/> and/or with its last step in <see cref="EstadoEjecucion"/>, the step
/// <see cref="PasoNombre"/> is run again, after <see cref="EsperaMinutos"/>. With a <see cref="Limite"/>, once a case has been reprocessed
/// that many times by this rule the next match does not reprocess it: the case is left on <see cref="EstadoFinalId"/> instead.
/// When several rules match the same end, only the first by <see cref="Orden"/> applies.
/// </summary>
public sealed class ReglaDeReprocesamiento
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FlujoId { get; set; }

    public required string Nombre { get; set; }

    public bool Activa { get; set; } = true;

    /// <summary>Lower goes first when several rules match.</summary>
    public int Orden { get; set; }

    /// <summary>The business state the case has to be in; null: any.</summary>
    public Guid? EstadoNegocioId { get; set; }

    /// <summary>How the case's last step ended: Completado, Fallido or Cancelado; null: any. At least one of this and
    /// <see cref="EstadoNegocioId"/> is set.</summary>
    public EjecucionPasoEstado? EstadoEjecucion { get; set; }

    /// <summary>The step of the process to run again, by name (names outlive versions); null: the step the case ended on.</summary>
    public string? PasoNombre { get; set; }

    /// <summary>0: right away (on the next turn of the reprocessor).</summary>
    public int EsperaMinutos { get; set; }

    /// <summary>How many times a case can be reprocessed by this rule; null: no limit.</summary>
    public int? Limite { get; set; }

    /// <summary>The business state a case is left on when the limit runs out; only with a <see cref="Limite"/>.</summary>
    public Guid? EstadoFinalId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public enum EstadoDeReprocesamiento
{
    /// <summary>Waiting for its moment.</summary>
    Pendiente,
    Hecho,
    /// <summary>It could not be run (the case never reached the step, …): <see cref="ReprocesamientoProgramado.Error"/> says why.</summary>
    Fallido,
    /// <summary>The case was already running again when its moment came (a person reprocessed it by hand): nothing to do.</summary>
    Descartado,
}

/// <summary>
/// One reprocessing a rule decided for a case: written in the same save as the case's end, run by the reprocessor when its moment comes.
/// The rows stay afterwards, so they are also what a rule's limit counts and what the process's screen lists.
/// </summary>
public sealed class ReprocesamientoProgramado
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FlujoId { get; set; }

    /// <summary>Null once the rule is deleted; <see cref="ReglaNombre"/> keeps saying which it was.</summary>
    public Guid? ReglaId { get; set; }

    public required string ReglaNombre { get; set; }

    public Guid CasoId { get; set; }

    /// <summary>The step to run again; null: the one the case ended on.</summary>
    public string? PasoNombre { get; set; }

    /// <summary>Which reprocessing of the case by this rule it is (1, 2, …).</summary>
    public int Numero { get; set; }

    public DateTimeOffset ProgramadoPara { get; set; }

    public EstadoDeReprocesamiento Estado { get; set; } = EstadoDeReprocesamiento.Pendiente;

    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? HechoAt { get; set; }
}
