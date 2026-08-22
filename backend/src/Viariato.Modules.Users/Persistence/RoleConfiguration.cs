using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Users.Domain;

namespace Viariato.Modules.Users.Persistence;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles", "users");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).IsRequired();

        builder.HasIndex(r => r.Name).IsUnique();
    }
}
