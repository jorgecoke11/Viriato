using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Persistence;

public sealed class FlujoParametroConfiguration : IEntityTypeConfiguration<FlujoParametro>
{
    public void Configure(EntityTypeBuilder<FlujoParametro> builder)
    {
        builder.ToTable("flujo_parametros", "flujos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Codigo).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Valor).IsRequired().HasMaxLength(2000);
        builder.Property(p => p.Descripcion).HasMaxLength(500);
        builder.Property(p => p.Etiqueta).HasMaxLength(100);

        builder.HasOne(p => p.Flujo)
            .WithMany()
            .HasForeignKey(p => p.FlujoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.FlujoId, p.Codigo }).IsUnique();
    }
}
