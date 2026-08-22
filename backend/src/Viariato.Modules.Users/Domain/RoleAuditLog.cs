namespace Viariato.Modules.Users.Domain;

public enum RoleAuditAction
{
    Granted,
    Revoked,
}

public sealed class RoleAuditLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? ActorUserId { get; set; }
    public Guid TargetUserId { get; set; }
    public Guid RoleId { get; set; }
    public RoleAuditAction Action { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
