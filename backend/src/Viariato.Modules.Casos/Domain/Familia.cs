namespace Viariato.Modules.Casos.Domain;

/// <summary>What happens when a family of this type ends.</summary>
public enum CondicionDeCierre
{
    /// <summary>The closing case opens however the members ended.</summary>
    Siempre,
    /// <summary>Only if every member ended completed: one failed or cancelled and the family ends without its closing case.</summary>
    SoloSiTodosBien,
}

/// <summary>
/// A kind of family a process works with (e.g. «Extracción del día»): the process's configuration, like its case types. It says what
/// is done when one of its families ends: open a case with <see cref="CreadorAlTerminarId"/> (of this or another process), handing it
/// the family's summary, under <see cref="Condicion"/>. Notification rules can also listen for its families ending.
/// </summary>
public sealed class TipoDeFamilia
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FlujoId { get; set; }

    /// <summary>Unique in its process: robots name it.</summary>
    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    public bool Activo { get; set; } = true;

    /// <summary>The creator of cases that opens the closing case when a family ends; null: no closing case.</summary>
    public Guid? CreadorAlTerminarId { get; set; }

    public CondicionDeCierre Condicion { get; set; } = CondicionDeCierre.Siempre;

    /// <summary>
    /// If set, its families form by themselves: the cases a case creates from its step with this name (by name, so it outlives versions)
    /// go into a family of this type that belongs to that case, opened with its first one; it closes when that step ends well. Null: the
    /// families are created by robots or people.
    /// </summary>
    public string? PasoQueLaForma { get; set; }

    /// <summary>With <see cref="PasoQueLaForma"/>: one family per case type of the cases created (a family per brand) instead of one for
    /// all of them.</summary>
    public bool UnaPorTipoDeCaso { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public enum EstadoDeFamilia
{
    /// <summary>Cases can still join it.</summary>
    Abierta,
    /// <summary>No more cases join; it ends when every member has ended.</summary>
    Cerrada,
    /// <summary>Every member ended: its summary is fixed and its actions have run.</summary>
    Terminada,
}

/// <summary>
/// A group of cases of a process that are seen, and end, together (e.g. «Extracción BSH 18/11»). It is <see cref="EstadoDeFamilia.Abierta"/>
/// while cases join it; once it is closed — by hand, by a robot, or by itself when it reaches <see cref="ParticipantesEsperados"/> — the
/// family finisher (<see cref="Familias.TerminadorDeFamilias"/>) ends it as soon as every member has ended and none is waiting to be
/// reprocessed, and runs what its type says. A case can belong to several families.
/// </summary>
public sealed class Familia
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FlujoId { get; set; }

    public Guid TipoDeFamiliaId { get; set; }

    public TipoDeFamilia? Tipo { get; set; }

    public required string Nombre { get; set; }

    public EstadoDeFamilia Estado { get; set; } = EstadoDeFamilia.Abierta;

    /// <summary>If set, the family closes by itself when it has this many members.</summary>
    public int? ParticipantesEsperados { get; set; }

    /// <summary>The members' end, counted when the family ends (null before).</summary>
    public int? Completados { get; set; }

    public int? Fallidos { get; set; }

    public int? Cancelados { get; set; }

    /// <summary>The case its type opened when it ended.</summary>
    public Guid? CasoDeCierreId { get; set; }

    /// <summary>Why the closing case could not be opened, or why it was not (condition not met).</summary>
    public string? NotaDeCierre { get; set; }

    public Guid? CreadaPorUserId { get; set; }

    /// <summary>The Despliegue whose robot created it, when it was a robot.</summary>
    public Guid? CreadaPorDespliegueId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? CerradaAt { get; set; }

    public DateTimeOffset? TerminadaAt { get; set; }

    public List<MiembroDeFamilia> Miembros { get; set; } = [];

    /// <summary>For a family that formed by itself: the case whose step created its members…</summary>
    public Guid? CasoOrigenId { get; set; }

    /// <summary>…the latest attempt of that step (the family closes when it ends well; a reprocess moves it to the new attempt)…</summary>
    public Guid? PasoOrigenId { get; set; }

    /// <summary>…and, for a type with <see cref="TipoDeFamilia.UnaPorTipoDeCaso"/>, the case type of its members (null: cases without one).</summary>
    public Guid? TipoCasoId { get; set; }
}

public sealed class MiembroDeFamilia
{
    public Guid FamiliaId { get; set; }

    public Guid CasoId { get; set; }

    public DateTimeOffset AnadidoAt { get; set; }
}

public enum FamiliaEventoAccion
{
    Creada,
    Cerrada,
    /// <summary><c>{ total, completados, fallidos, cancelados }</c>. Notification rules listening for the family's type fire on it.</summary>
    Terminada,
    /// <summary><c>{ casoId, creador }</c>.</summary>
    CierreAbierto,
    /// <summary>The closing case was not opened: <c>{ motivo }</c>.</summary>
    CierreNoAbierto,
    /// <summary>A notice about the family went out (<c>{ regla, destinatarios, asunto }</c>).</summary>
    NotificacionEnviada,
    /// <summary>A notice about the family could not be sent after every attempt (<c>{ regla, destinatarios, asunto, error }</c>).</summary>
    NotificacionFallida,
}

/// <summary>The family's history, append-only like a case's: what the front shows and what notification rules listen to.</summary>
public sealed class FamiliaEvento
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FamiliaId { get; set; }

    public FamiliaEventoAccion Accion { get; set; }

    public string? DetalleJson { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
