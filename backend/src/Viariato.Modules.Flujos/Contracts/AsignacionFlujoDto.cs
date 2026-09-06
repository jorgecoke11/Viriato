namespace Viariato.Modules.Flujos.Contracts;

public sealed record AsignacionFlujoDto(Guid Id, Guid FlujoId, Guid UserId, DateTimeOffset CreatedAt);

public sealed record CreateAsignacionRequest(Guid UserId);
