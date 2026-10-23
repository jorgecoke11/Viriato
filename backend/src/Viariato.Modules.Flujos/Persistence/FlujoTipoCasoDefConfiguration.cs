using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Persistence;

public sealed class FlujoTipoCasoDefConfiguration : IEntityTypeConfiguration<FlujoTipoCasoDef>
{
    public void Configure(EntityTypeBuilder<FlujoTipoCasoDef> builder)
    {
        builder.ToTable("flujo_tipo_caso_defs", "flujos");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Nombre).IsRequired().HasMaxLength(100);

        builder.HasOne(t => t.Flujo)
            .WithMany()
            .HasForeignKey(t => t.FlujoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.FlujoId, t.Nombre }).IsUnique();
    }
}
