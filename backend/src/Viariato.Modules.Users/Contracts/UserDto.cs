namespace Viariato.Modules.Users.Contracts;

public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string BaseCurrency,
    string TimeZone,
    string Locale,
    bool IsActive,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTimeOffset CreatedAt);
