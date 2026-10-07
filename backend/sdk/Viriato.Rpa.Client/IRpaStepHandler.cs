namespace Viriato.Rpa.Client;

/// <summary>
/// What a robot implements: given one claimed step, do the automation and say how it ended. This is the
/// only thing specific to a robot — claiming, reporting, retries and shutdown belong to the library.
/// Registered as a singleton, so keep per-run state inside the call (or a factory it uses), not in fields.
/// </summary>
public interface IRpaStepHandler
{
    /// <summary>Throwing fails the step with the exception's message; <see cref="OperationCanceledException"/>
    /// caused by <paramref name="ct"/> means the robot is stopping and the step is reported as interrupted.</summary>
    Task<RpaStepResult> EjecutarAsync(RpaStepContext paso, CancellationToken ct);
}
