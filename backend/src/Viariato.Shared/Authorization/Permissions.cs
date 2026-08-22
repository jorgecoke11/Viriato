namespace Viariato.Shared.Authorization;

public static class Permissions
{
    public const string UsersRead = "users.read";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";

    public static IReadOnlyList<string> All { get; } = [UsersRead, UsersManage, RolesManage];
}
