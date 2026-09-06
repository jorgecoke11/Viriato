using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Persistence;

public sealed class FlujoVersionConfiguration : IEntityTypeConfiguration<FlujoVersion>
{
    public void Configure(EntityTypeBuilder<FlujoVersion> builder)
    {
        builder.ToTable("flujo_versiones", "flujos");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(v => v.Notas).HasMaxLength(500);

        builder.HasOne(v => v.Flujo)
            .WithMany(f => f.Versiones)
            .HasForeignKey(v => v.FlujoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.Pasos)
            .WithOne(p => p.FlujoVersion)
            .HasForeignKey(p => p.FlujoVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => new { v.FlujoId, v.NumeroVersion }).IsUnique();
        builder.HasIndex(v => v.FlujoId);
        builder.HasIndex(v => v.Estado);
    }
}
