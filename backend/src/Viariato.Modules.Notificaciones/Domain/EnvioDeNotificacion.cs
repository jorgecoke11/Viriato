namespace Viariato.Modules.Notificaciones.Domain;

public enum EstadoDeEnvio
{
    Pendiente,
    Enviado,
    Fallido,
}

/// <summary>
/// One notice to send — the outbox. A rule that fires only writes one of these, in the same transaction as what made it fire, and the
/// postman (CarteroDeNotificaciones) sends it afterwards, retrying with growing waits. So a mail server that is down never slows down
/// or breaks a case, and nothing is lost if the API stops halfway. Subject and body are already written when it is queued: what goes out
/// is what the case looked like at that moment.
/// </summary>
public sealed class EnvioDeNotificacion
{
    public const int MaximoDeIntentos = 5;

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FlujoId { get; set; }

    /// <summary>Null once the rule is deleted: what was sent stays in the list.</summary>
    public Guid? ReglaId { get; set; }

    public required string ReglaNombre { get; set; }

    public Guid CasoId { get; set; }

    public Guid ConexionId { get; set; }

    public List<string> Destinatarios { get; set; } = [];

    public required string Asunto { get; set; }

    public required string Cuerpo { get; set; }

    public EstadoDeEnvio Estado { get; set; } = EstadoDeEnvio.Pendiente;

    public int Intentos { get; set; }

    public string? UltimoError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ProximoIntentoAt { get; set; }

    public DateTimeOffset? EnviadoAt { get; set; }
}
