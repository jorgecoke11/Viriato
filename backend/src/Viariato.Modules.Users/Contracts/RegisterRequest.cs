namespace Viariato.Modules.Users.Contracts;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
