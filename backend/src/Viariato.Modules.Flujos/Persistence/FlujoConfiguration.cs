using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Persistence;

public sealed class FlujoConfiguration : IEntityTypeConfiguration<Flujo>
{
    public void Configure(EntityTypeBuilder<Flujo> builder)
    {
        builder.ToTable("flujos", "flujos");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Nombre).IsRequired().HasMaxLength(200);
        builder.Property(f => f.Descripcion).HasMaxLength(1000);

        builder.HasOne(f => f.VersionActiva)
            .WithMany()
            .HasForeignKey(f => f.VersionActivaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(f => f.Versiones)
            .WithOne(v => v.Flujo)
            .HasForeignKey(v => v.FlujoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.Nombre);
        builder.HasIndex(f => f.VersionActivaId);
    }
}
