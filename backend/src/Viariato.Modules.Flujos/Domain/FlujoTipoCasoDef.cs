namespace Viariato.Modules.Flujos.Domain;

/// <summary>
/// A category the user defines per Flujo to classify its Casos (e.g. "Alta simple" vs "Alta
/// compleja") — orthogonal to <see cref="FlujoEstadoDef"/>: a Tipo groups many Casos regardless of
/// where each one currently stands, while an Estado describes one Caso's current position. Chosen
/// once, when a Caso is created; never reassigned by the engine.
/// </summary>
public sealed class FlujoTipoCasoDef
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FlujoId { get; set; }

    public Flujo? Flujo { get; set; }

    public required string Nombre { get; set; }

    public int Orden { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>The form of this type's business data, as a schema (see <c>EsquemaDatos</c>), or null when the data is
    /// free-form JSON. Kept as text rather than jsonb on purpose: jsonb reorders keys, and the order of
    /// <c>properties</c> is the order of the fields in the form.</summary>
    public string? EsquemaDatosJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
