using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Persistence;

public sealed class StorageConfigConfiguration : IEntityTypeConfiguration<StorageConfig>
{
    public void Configure(EntityTypeBuilder<StorageConfig> builder)
    {
        builder.ToTable("storage_configs", "flujos");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Nombre).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Endpoint).HasMaxLength(500);
        builder.Property(s => s.Region).HasMaxLength(100);
        builder.Property(s => s.BucketName).HasMaxLength(200);
        builder.Property(s => s.AccessKey).HasMaxLength(500);
        builder.Property(s => s.SecretKey).HasMaxLength(500);
        builder.Property(s => s.LocalPath).HasMaxLength(500);

        builder.HasIndex(s => s.Nombre).IsUnique();
    }
}
