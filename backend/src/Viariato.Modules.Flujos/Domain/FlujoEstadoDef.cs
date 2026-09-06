namespace Viariato.Modules.Flujos.Domain;

/// <summary>
/// A business-facing status the user defines per Flujo (e.g. "En banco", "Rechazado por cliente") —
/// entirely separate from <see cref="Casos.Domain.CasoEstado"/>, which stays the engine's own
/// technical lifecycle (Iniciado/EnProgreso/Pausado/etc.) and never becomes user-configurable.
/// <see cref="Codigo"/> is the stable identifier a step's own script/config references when it wants
/// to report "the case is now in this business state"; <see cref="Display"/> is what users see.
/// </summary>
public sealed class FlujoEstadoDef
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FlujoId { get; set; }

    public Flujo? Flujo { get; set; }

    public required string Codigo { get; set; }

    public required string Display { get; set; }

    public int Orden { get; set; }

    /// <summary>Marks this as an end-of-the-line business outcome (e.g. "Completado", "Rechazado") —
    /// purely descriptive for how the UI presents it, never consulted by the engine.</summary>
    public bool EsFinal { get; set; }

    public bool Activo { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
