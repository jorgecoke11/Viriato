namespace Viariato.Modules.Flujos.Domain;

/// <summary>
/// A creator that fires by itself. Its <see cref="Cron"/> says when; at each of those moments Viriato opens a Caso with the creator's
/// type, step, estado and title — exactly as if a person had picked it — and the data written here as its input. It is configuration of
/// the process, like the creators it uses, so it belongs to the Flujo and goes with it. This is what starts the work that has to happen
/// every day ("analyse the list at 10:00") without a robot whose only job is to look at the clock.
/// </summary>
public sealed class LanzadorDeCaso
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FlujoId { get; set; }

    public Flujo? Flujo { get; set; }

    /// <summary>The creator that is fired: it decides what the Caso is. Deleting the creator deletes its launchers.</summary>
    public Guid CreadorDeCasoId { get; set; }

    public CreadorDeCaso? CreadorDeCaso { get; set; }

    public required string Nombre { get; set; }

    public string? Descripcion { get; set; }

    /// <summary>The input of every Caso it opens (a JSON object, checked against the creator's type). Null: no data.</summary>
    public string? DatosJson { get; set; }

    /// <summary>When it fires: crontab expressions of five fields (minute, hour, day of month, month, day of week), one per line;
    /// it fires when any of them matches. "0 10 * * 1-5" is every weekday at 10:00.</summary>
    public required string Cron { get; set; }

    /// <summary>The time zone the cron is read in (IANA name, e.g. "America/New_York"): a market's hours are its own, not the server's.</summary>
    public required string ZonaHoraria { get; set; }

    /// <summary>How late a moment can still be acted on. After a long stop (the API was down) the moments that went by are skipped rather
    /// than all fired at once on stale ground.</summary>
    public int ToleranciaMinutos { get; set; } = 120;

    /// <summary>A paused launcher does nothing; nothing is deleted behind it.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>The moment (UTC) of the last time it fired, or tried to: what stops the same moment from firing twice, across restarts and replicas.</summary>
    public DateTimeOffset? UltimoDisparoEn { get; set; }

    public Guid? UltimoCasoId { get; set; }

    /// <summary>Why the last attempt could not open a Caso, or null if it did. Shown next to the launcher so a person can fix it.</summary>
    public string? UltimoError { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
