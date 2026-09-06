namespace Viariato.Modules.Casos.Domain;

/// <summary>1:1 structured detail for an EjecucionPaso whose TipoPaso is Agente — keeps decisions,
/// tool calls and input/output snapshots queryable instead of buried in a generic jsonb blob.</summary>
public sealed class AgenteEjecucionDetalle
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EjecucionPasoId { get; set; }

    public EjecucionPaso? EjecucionPaso { get; set; }

    public required string Modelo { get; set; }

    /// <summary>jsonb: what the agent evaluated, what it chose, why.</summary>
    public string? DecisionesJson { get; set; }

    /// <summary>jsonb array of tool names actually invoked.</summary>
    public string? HerramientasUsadas { get; set; }

    public string? InputSnapshot { get; set; }

    public string? OutputSnapshot { get; set; }
}
