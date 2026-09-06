namespace Viariato.Modules.Casos.Contracts;

public sealed record CasoEventoDto(
    Guid Id,
    Guid? ActorUserId,
    Guid? EjecucionId,
    string Accion,
    string? DetalleJson,
    DateTimeOffset OccurredAt);
