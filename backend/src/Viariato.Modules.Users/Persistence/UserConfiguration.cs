using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Users.Domain;

namespace Viariato.Modules.Users.Persistence;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", "users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Email).IsRequired();
        builder.Property(u => u.EmailNormalized).IsRequired();
        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.DisplayName).IsRequired();
        builder.Property(u => u.BaseCurrency).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(u => u.TimeZone).IsRequired();
        builder.Property(u => u.Locale).IsRequired();

        builder.HasIndex(u => u.EmailNormalized).IsUnique();
    }
}
