using Viariato.Modules.Users.Domain;

namespace Viariato.Modules.Users.Contracts;

internal static class UserDtoMapper
{
    public static UserDto ToDto(this User user, IReadOnlyList<string> roles, IReadOnlyList<string> permissions) =>
        new(
            user.Id,
            user.Email,
            user.DisplayName,
            user.BaseCurrency,
            user.TimeZone,
            user.Locale,
            roles,
            permissions,
            user.CreatedAt);
}
