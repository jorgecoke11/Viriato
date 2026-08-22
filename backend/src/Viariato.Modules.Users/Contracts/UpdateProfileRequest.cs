namespace Viariato.Modules.Users.Contracts;

public sealed record UpdateProfileRequest(string? DisplayName, string? BaseCurrency, string? TimeZone, string? Locale);
