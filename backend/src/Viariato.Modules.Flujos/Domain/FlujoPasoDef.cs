namespace Viariato.Modules.Flujos.Domain;

/// <summary>One step definition within a <see cref="FlujoVersion"/>. <see cref="ConfiguracionJson"/>'s
/// shape depends on <see cref="TipoPaso"/> (documented convention, not DB-enforced): Rpa needs
/// aplicacion/script/timeoutSeconds, Api needs url/method/headersTemplate, Decision needs
/// condicion/ordenSiVerdadero/ordenSiFalso, Espera needs duracionMinutos or condicionReanudacion,
/// RevisionHumana needs instrucciones — Interno/Agente are free-form for their executor.</summary>
public sealed class FlujoPasoDef
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FlujoVersionId { get; set; }

    public FlujoVersion? FlujoVersion { get; set; }

    /// <summary>1-based position within the version. Linear by default; a Decision step's
    /// <see cref="ConfiguracionJson"/> can send execution to a different Orden.</summary>
    public int Orden { get; set; }

    public required string Nombre { get; set; }

    public TipoPaso TipoPaso { get; set; }

    /// <summary>Only meaningful when <see cref="TipoPaso"/> is <see cref="Domain.TipoPaso.Agente"/>.</summary>
    public Guid? AgenteDefinicionId { get; set; }

    public AgenteDefinicion? AgenteDefinicion { get; set; }

    /// <summary>Only meaningful when TipoPaso is Rpa — which registered servicio is responsible for
    /// this step, and therefore whose queue it lands in.</summary>
    public Guid? ServicioId { get; set; }

    public RpaFleet.Domain.Servicio? Servicio { get; set; }

    public string? ConfiguracionJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
