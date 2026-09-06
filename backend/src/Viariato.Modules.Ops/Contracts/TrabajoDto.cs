namespace Viariato.Modules.Ops.Contracts;

public sealed record TrabajoListItemDto(
    Guid Id,
    string ProcessCode,
    string Status,
    string? SubjectType,
    string? SubjectKey,
    Guid? ParentTrabajoId,
    int? Progress,
    string? Summary,
    string? Error,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);

public sealed record TrabajoLogDto(Guid Id, DateTimeOffset Timestamp, string Level, string Message);

public sealed record TrabajoDetailDto(
    Guid Id,
    string ProcessCode,
    string Status,
    string? SubjectType,
    string? SubjectKey,
    Guid? ParentTrabajoId,
    int? Progress,
    string? Summary,
    string? Data,
    string? Error,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<TrabajoLogDto> Logs,
    IReadOnlyList<TrabajoListItemDto> Children);
