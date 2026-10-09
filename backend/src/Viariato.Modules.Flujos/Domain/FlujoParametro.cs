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

    /// <summary>Whether the people who work the process's cases may change the value themselves, from the dashboard. Off by default:
    /// most settings (a selector, a URL…) are for whoever manages the process, and only the ones that are a business decision
    /// (an IVA, a coupon, the list of products to extract) are opened up. It changes who may write the value, nothing about how a
    /// robot reads it.</summary>
    public bool EditablePorUsuario { get; set; }

    /// <summary>What a person sees instead of the code when the parameter is editable from the dashboard ("IVA (%)" instead of
    /// <c>iva</c>). Null: the code.</summary>
    public string? Etiqueta { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
