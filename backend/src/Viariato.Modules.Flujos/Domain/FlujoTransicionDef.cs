namespace Viariato.Modules.Flujos.Domain;

/// <summary>How the execution of a step ended, as an arrow of the flow looks at it.</summary>
public enum ResultadoEjecucion
{
    Completada,
    Fallida,
}

/// <summary>
/// One arrow of a <see cref="FlujoVersion"/>'s flow: when the step <see cref="PasoOrigenId"/> (in practice, its servicio) ends with
/// <see cref="Resultado"/> and the Caso is in the business state <see cref="EstadoNegocioId"/>, the Caso goes on to
/// <see cref="PasoDestinoId"/>. A null origin is the flow's start: the step a new Caso begins with. A null state means "any state",
/// and loses against an arrow naming the Caso's exact state. With no arrow that applies, the Caso ends with the execution's result.
/// A version with no arrows at all is an older, list-shaped one: it keeps moving by <see cref="FlujoPasoDef.Orden"/>.
/// Frozen with the version when it is published, like its steps.
/// </summary>
public sealed class FlujoTransicionDef
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FlujoVersionId { get; set; }

    public FlujoVersion? FlujoVersion { get; set; }

    /// <summary>Null for the start arrow.</summary>
    public Guid? PasoOrigenId { get; set; }

    public FlujoPasoDef? PasoOrigen { get; set; }

    /// <summary>Ignored on the start arrow, which is stored as <see cref="ResultadoEjecucion.Completada"/>.</summary>
    public ResultadoEjecucion Resultado { get; set; }

    /// <summary>Null for "any state".</summary>
    public Guid? EstadoNegocioId { get; set; }

    public FlujoEstadoDef? EstadoNegocio { get; set; }

    public Guid PasoDestinoId { get; set; }

    public FlujoPasoDef? PasoDestino { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
