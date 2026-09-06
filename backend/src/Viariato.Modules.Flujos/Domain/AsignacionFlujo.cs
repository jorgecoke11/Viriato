namespace Viariato.Modules.Flujos.Domain;

/// <summary>
/// A real security boundary, not a UI preference: a user can only see/act on Casos of a Flujo they
/// have a row here for. Enforced by Casos on every endpoint that reads or mutates a Caso — this
/// entity only records the grant, it doesn't check anything itself.
/// </summary>
public sealed class AsignacionFlujo
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FlujoId { get; set; }

    public Flujo? Flujo { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }
}
