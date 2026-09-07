namespace Viariato.Modules.Users.Contracts;

public sealed record CreateUserRequest(string Email, string Password, string DisplayName);
