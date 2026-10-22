namespace Viariato.Modules.Flujos.Domain;

/// <summary>
/// The form of a Caso's business data (see <c>EsquemaDatos</c>): which fields it has and what each accepts. It belongs to the process
/// and is chosen by its creators — "Alta" and "Alta urgente" can share one, and a change to it is made once. A Caso remembers the form
/// it was born with, so its data are checked against it when edited later.
/// </summary>
public sealed class Formulario
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FlujoId { get; set; }

    public Flujo? Flujo { get; set; }

    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>The schema. Kept as text rather than jsonb on purpose: jsonb reorders keys, and the order of <c>properties</c> is the
    /// order of the fields in the form.</summary>
    public required string EsquemaJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
