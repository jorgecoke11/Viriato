namespace Viariato.Modules.Users.Domain;

public sealed class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public DateTimeOffset GrantedAt { get; set; }
    public Guid? GrantedBy { get; set; }
}
