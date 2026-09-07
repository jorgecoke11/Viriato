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
}
