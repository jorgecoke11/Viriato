namespace Viariato.Infrastructure.Trabajos;

public enum TrabajoLogLevel
{
    Info,
    Warning,
    Error,
}

/// <summary>One log line reported by a <see cref="Trabajo"/> while it runs — what makes the platform feel like a live monitor, not just a results table.</summary>
public sealed class TrabajoLog
{
    public Guid Id { get; set; }

    public Guid TrabajoId { get; set; }

    public Trabajo? Trabajo { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public TrabajoLogLevel Level { get; set; }

    public required string Message { get; set; }
}
