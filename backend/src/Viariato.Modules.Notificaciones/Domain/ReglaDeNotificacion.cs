namespace Viariato.Modules.Notificaciones.Domain;

/// <summary>What has to happen to a case for a notice to go out.</summary>
public enum DisparadorDeNotificacion
{
    /// <summary>A robot said so (<c>POST …/notificar</c>), with this <see cref="ReglaDeNotificacion.Codigo"/> as the event.</summary>
    EventoDelRobot,
    /// <summary>The case reached the business state <see cref="ReglaDeNotificacion.Codigo"/>.</summary>
    EstadoNegocio,
    CasoFallido,
    CasoCompletado,
}

/// <summary>
/// A notification rule of a process: when <see cref="Disparador"/> happens to one of its cases (of <see cref="TipoCasoId"/>, or of any
/// type), send <see cref="Asunto"/> / <see cref="Cuerpo"/> to <see cref="Destinatarios"/> through the account <see cref="ConexionId"/>.
/// Subject and body are templates like a case title: <c>{caso}</c>, <c>{proceso}</c>, <c>{tipo}</c>, <c>{estado}</c>, <c>{fecha}</c>,
/// <c>{hora}</c>, <c>{enlace}</c>, <c>{datos.campo}</c> (the case's data) and <c>{evento.campo}</c> (what the robot sent).
/// </summary>
public sealed class ReglaDeNotificacion
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FlujoId { get; set; }

    /// <summary>Null: any type of case of the process.</summary>
    public Guid? TipoCasoId { get; set; }

    public required string Nombre { get; set; }

    public bool Activa { get; set; } = true;

    public DisparadorDeNotificacion Disparador { get; set; }

    /// <summary>The robot's event or the business state's code; null for the other triggers.</summary>
    public string? Codigo { get; set; }

    public Guid ConexionId { get; set; }

    /// <summary>The addresses (a text[] column).</summary>
    public List<string> Destinatarios { get; set; } = [];

    public required string Asunto { get; set; }

    public required string Cuerpo { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
