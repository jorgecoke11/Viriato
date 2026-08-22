namespace Viariato.Modules.Users.Contracts;

public sealed record AuthResponse(string AccessToken, int ExpiresIn, UserDto User);
