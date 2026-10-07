namespace Viariato.Modules.Flujos.Domain;

/// <summary>
/// A named setting of one Flujo (e.g. <c>iva</c> = 21, <c>n_maximo_carrito</c> = 14) that the robots of that
/// process read at run time, instead of each robot carrying its own configuration. Plain text, visible to
/// anyone who can read the process: passwords and tokens belong in credentials, never here.
/// <see cref="Codigo"/> is how a robot asks for it, so it never changes once created.
/// </summary>
public sealed class FlujoParametro
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FlujoId { get; set; }

    public Flujo? Flujo { get; set; }

    /// <summary>Lowercase key, unique within the Flujo.</summary>
    public required string Codigo { get; set; }

    /// <summary>May be empty: an empty value is a legitimate setting (e.g. "no coupon").</summary>
    public required string Valor { get; set; }

    public string? Descripcion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
