namespace Viriato.Rpa.Client;

public enum RpaStepOutcome
{
    Completado,
    CasoCompletado,
    Fallido,
}

/// <summary>What a robot's handler tells <see cref="RpaWorker"/> to report for the step it just ran.</summary>
public sealed record RpaStepResult
{
    private RpaStepResult(RpaStepOutcome outcome, string? parametrosSalida, string? error)
    {
        Outcome = outcome;
        ParametrosSalida = parametrosSalida;
        Error = error;
    }

    public RpaStepOutcome Outcome { get; }

    public string? ParametrosSalida { get; }

    public string? Error { get; }

    /// <summary>The step finished; the Caso moves on to its next step (or finishes if there is none).</summary>
    public static RpaStepResult Completado(string? parametrosSalida = null) => new(RpaStepOutcome.Completado, parametrosSalida, null);

    /// <summary>The step finished AND it is the true end of the cycle: the whole Caso is closed now,
    /// even if the Flujo defines more steps after this one.</summary>
    public static RpaStepResult CasoCompletado(string? parametrosSalida = null) => new(RpaStepOutcome.CasoCompletado, parametrosSalida, null);

    public static RpaStepResult Fallido(string error) => new(RpaStepOutcome.Fallido, null, error);
}
