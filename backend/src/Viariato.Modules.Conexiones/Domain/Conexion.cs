namespace Viariato.Modules.Conexiones.Domain;

/// <summary>
/// An account of the platform with something outside it: a mailbox to send mail from today, a Telegram bot or an API tomorrow. What
/// kind of account it is (<see cref="Tipo"/>) decides its settings and what it can do (see <c>ITipoDeConexion</c>). Anything that
/// needs to talk to the outside world — notifications first, processes later — picks one by name instead of keeping its own.
/// The password lives apart from the settings, encrypted, and no response ever carries it.
/// </summary>
public sealed class Conexion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Nombre { get; set; }

    /// <summary>The code of its kind, e.g. <c>correo</c>.</summary>
    public required string Tipo { get; set; }

    /// <summary>jsonb: the settings its kind asks for (server, port, sender…), nothing secret.</summary>
    public required string ConfiguracionJson { get; set; }

    /// <summary>The password or token, encrypted (AES-GCM bound to <see cref="Id"/>); null if its kind needs none or none was given.</summary>
    public string? SecretoCifrado { get; set; }

    public bool Activa { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
