namespace Viariato.Modules.Users.Contracts;

public sealed record UpdateUserRequest(string? DisplayName, bool? IsActive);
