using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Persistence;

public sealed class FlujoEstadoDefConfiguration : IEntityTypeConfiguration<FlujoEstadoDef>
{
    public void Configure(EntityTypeBuilder<FlujoEstadoDef> builder)
    {
        builder.ToTable("flujo_estado_defs", "flujos");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Codigo).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Display).IsRequired().HasMaxLength(100);

        builder.HasOne(e => e.Flujo)
            .WithMany()
            .HasForeignKey(e => e.FlujoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.FlujoId, e.Codigo }).IsUnique();
    }
}
