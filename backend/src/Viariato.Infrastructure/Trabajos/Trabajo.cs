namespace Viariato.Infrastructure.Trabajos;

/// <summary>
/// A generic unit of automated work (a scraping run, a script, an analysis) — the single dynamic
/// entity every module reports its background activity through, instead of each module owning its
/// own bespoke result tables. Whatever business data a process produces lives in <see cref="Data"/>
/// as JSON; the platform never needs a migration to support a new kind of job.
/// </summary>
public sealed class Trabajo
{
    public Guid Id { get; set; }

    /// <summary>Free-form process identifier declared in code by the owning module, e.g. "markets.sync".</summary>
    public required string ProcessCode { get; set; }

    public TrabajoStatus Status { get; set; } = TrabajoStatus.Pending;

    /// <summary>Optional grouping of what this Trabajo is about, e.g. "company" / "AAPL".</summary>
    public string? SubjectType { get; set; }

    public string? SubjectKey { get; set; }

    public Guid? ParentTrabajoId { get; set; }

    public Trabajo? ParentTrabajo { get; set; }

    public int? Progress { get; set; }

    public string? Summary { get; set; }

    /// <summary>Dynamic business payload for this Trabajo, stored as jsonb.</summary>
    public string? Data { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}
