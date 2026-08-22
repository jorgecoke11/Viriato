namespace Viariato.Modules.Users.Domain;

public sealed class Role
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
