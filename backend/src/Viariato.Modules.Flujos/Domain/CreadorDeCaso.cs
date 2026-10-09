namespace Viariato.Modules.Flujos.Domain;

/// <summary>
/// A ready-made way to start a Caso of one Flujo: everything a person should not have to decide — which type of case it is,
/// which service it starts at, which business estado it is born in, how its title is written — is configured here once. To the
/// user, creating a case is picking a creator and filling in its data. A Flujo can have as many as it needs ("Alta de cliente",
/// "Alta urgente"…); they are configuration of the process, so they belong to it and go with it.
/// </summary>
public sealed class CreadorDeCaso
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FlujoId { get; set; }

    public Flujo? Flujo { get; set; }

    /// <summary>What the user sees in the picker.</summary>
    public required string Nombre { get; set; }

    /// <summary>One line under the name to tell it apart from its siblings.</summary>
    public string? Descripcion { get; set; }

    /// <summary>The type the Caso gets, and with it the form of its data (the type's schema). Null: no type, free-form JSON.</summary>
    public Guid? TipoCasoId { get; set; }

    public FlujoTipoCasoDef? TipoCaso { get; set; }

    /// <summary>The step the Caso starts at, by name rather than by id: versions of a Flujo each have their own step rows, and a
    /// creator must keep working when a new version is published. Null: the first step of the flow.</summary>
    public string? PasoInicialNombre { get; set; }

    public Guid? EstadoNegocioInicialId { get; set; }

    public FlujoEstadoDef? EstadoNegocioInicial { get; set; }

    /// <summary>How the title is written when the user does not choose one (see <c>PlantillaDeTitulo</c> for the placeholders).</summary>
    public required string PlantillaTitulo { get; set; }

    /// <summary>How many cases this creator has numbered so far: the <c>{n}</c> of the title. Only ever moved by the database
    /// (<c>update … returning</c>), so two cases created at once never get the same number.</summary>
    public long Secuencia { get; set; }

    public int Orden { get; set; }

    /// <summary>A retired creator is not offered any more; nothing is deleted behind it.</summary>
    public bool Activo { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
