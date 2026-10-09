namespace Viariato.Modules.Casos.Domain;

/// <summary>1:1 structured detail for an EjecucionPaso whose TipoPaso is Rpa.</summary>
public sealed class RpaEjecucionDetalle
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EjecucionPasoId { get; set; }

    public EjecucionPaso? EjecucionPaso { get; set; }

    public required string AplicacionObjetivo { get; set; }

    public string? WorkerId { get; set; }

    public string? ParametrosEntrada { get; set; }

    public string? ParametrosSalida { get; set; }

    /// <summary>Set when a worker claims this step off the queue (POST /api/v1/rpa/cola/siguiente).
    /// Null means it's still pending. Bare Guid, not FK-constrained — RpaFleet.Despliegue lives in a
    /// different module, same convention as AsignacionFlujo.UserId.</summary>
    public Guid? DespliegueId { get; set; }

    public DateTimeOffset? ClaimedAt { get; set; }

    /// <summary>Which running copy of the robot claimed it (see <c>InstanciaDeDespliegue</c>); null when the robot did not
    /// say, which is the case for robots written before instances existed.</summary>
    public string? InstanciaId { get; set; }

    /// <summary>
    /// Which of the executions waiting for the same service goes first: the higher the number, the sooner. Every
    /// execution is created at 0 — only a robot opening a Caso can choose another for the one it creates — and a person can
    /// change it while it waits. It only orders the queue of one service: the machine's own order of services is
    /// decided first. Negative values wait behind the ordinary ones. It means nothing once a robot has claimed it.
    /// </summary>
    public int Prioridad { get; set; }

    /// <summary>How far along the robot says it is (0–100), while it runs the step. Only what the robot reports: the platform
    /// does not guess it. Null until it says something.</summary>
    public int? ProgresoPorcentaje { get; set; }

    /// <summary>What the robot is doing right now, in its own words ("Añadiendo productos a la cesta").</summary>
    public string? ProgresoMensaje { get; set; }

    public DateTimeOffset? ProgresoAt { get; set; }

    /// <summary>Where the robot's screen can be watched while the step runs (a web page, for instance noVNC). Reported by the
    /// robot when it starts and meaningless once the step ends. Always an http(s) address: it ends up as a link in the interface.</summary>
    public string? VistaEnDirectoUrl { get; set; }
}
