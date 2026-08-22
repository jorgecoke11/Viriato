using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Users.Domain;

namespace Viariato.Modules.Users.Persistence;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions", "users");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired();
        builder.Property(p => p.Module).IsRequired();
        builder.Property(p => p.Action).IsRequired();

        builder.HasIndex(p => p.Name).IsUnique();
    }
}
