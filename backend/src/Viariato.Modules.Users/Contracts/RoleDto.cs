namespace Viariato.Modules.Users.Contracts;

public sealed record RoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystem,
    IReadOnlyList<string> Permissions,
    DateTimeOffset CreatedAt);

public sealed record CreateRoleRequest(string Name, string? Description);

public sealed record UpdateRoleRequest(string? Name, string? Description);

public sealed record UpdateRolePermissionsRequest(IReadOnlyList<Guid> PermissionIds);

public sealed record PermissionDto(Guid Id, string Name, string Module, string Action, string? Description);
