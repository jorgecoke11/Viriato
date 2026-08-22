namespace Viariato.Modules.Users.Contracts;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
