namespace Viariato.Modules.RpaFleet.Domain;

/// <summary>
/// A secret a robot needs to log into some system (a portal user and password, an API token). Robots ask for
/// it by <see cref="Nombre"/>, so the name is the lookup key and never changes once created. The password is
/// stored only encrypted and is write-only from the admin side: nothing in the API hands it back except the
/// robot-facing endpoint.
/// </summary>
public sealed class Credencial
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Lowercase, e.g. "tradeplace". What the robot passes to ask for it.</summary>
    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>Not secret on its own, so it is shown in the admin list. Null for token-only credentials.</summary>
    public string? Usuario { get; set; }

    public required string PasswordCifrado { get; set; }

    /// <summary>Null = any robot may read it; otherwise only the Despliegues of this Servicio. Deleting a
    /// Servicio that still has credentials is refused, never turned into "any robot".</summary>
    public Guid? ServicioId { get; set; }

    public Servicio? Servicio { get; set; }

    /// <summary>An inactive credential is not served, as if it did not exist.</summary>
    public bool Activo { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? UltimoAccesoAt { get; set; }
}
