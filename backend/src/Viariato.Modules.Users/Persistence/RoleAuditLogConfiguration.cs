using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Users.Domain;

namespace Viariato.Modules.Users.Persistence;

public sealed class RoleAuditLogConfiguration : IEntityTypeConfiguration<RoleAuditLog>
{
    public void Configure(EntityTypeBuilder<RoleAuditLog> builder)
    {
        builder.ToTable("role_audit_log", "users");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Action)
            .HasConversion(
                action => action == RoleAuditAction.Granted ? "granted" : "revoked",
                value => value == "granted" ? RoleAuditAction.Granted : RoleAuditAction.Revoked)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(l => l.TargetUserId);
    }
}
