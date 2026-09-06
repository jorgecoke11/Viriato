namespace Viariato.Modules.Casos.Contracts;

public sealed record RevisionHumanaDto(
    Guid Id,
    Guid EjecucionPasoId,
    Guid RevisorUserId,
    string Decision,
    string? Comentario,
    DateTimeOffset ResolvedAt);

public sealed record ResolverRevisionRequest(string Decision, string? Comentario);

public sealed record RevisionPendienteDto(
    Guid EjecucionPasoId,
    Guid CasoId,
    string CasoTitulo,
    Guid FlujoPasoDefId,
    DateTimeOffset? StartedAt);
