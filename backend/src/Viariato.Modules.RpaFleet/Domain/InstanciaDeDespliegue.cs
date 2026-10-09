namespace Viariato.Modules.RpaFleet.Domain;

/// <summary>
/// One running copy of a Despliegue's robot — a replica of the container, or one more process started on Windows with the
/// same API key. The platform never configures how many there are: an instance announces itself the first time it asks for
/// work (with an id it generated when it started) and is there for as long as it keeps asking. An instance that holds a step
/// is busy; one that does not is a free slot. So capacity is whatever the stack runs, and a stuck instance only takes its own
/// slot, never the others'.
///
/// A robot that does not send an id (an older client) is one instance with the empty id, which is exactly how it behaved
/// before instances existed.
/// </summary>
public sealed class InstanciaDeDespliegue
{
    /// <summary>The id of an instance that did not give one.</summary>
    public const string SinId = "";

    public const int LongitudMaxima = 64;

    public Guid DespliegueId { get; set; }

    public required string InstanciaId { get; set; }

    /// <summary>The last time it asked for work. Past the dispatcher's connection window it no longer counts as there.</summary>
    public DateTimeOffset LastSeenAt { get; set; }
}
