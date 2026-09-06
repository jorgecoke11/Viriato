using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.Persistence;

public sealed class CasoEventoConfiguration : IEntityTypeConfiguration<CasoEvento>
{
    public void Configure(EntityTypeBuilder<CasoEvento> builder)
    {
        builder.ToTable("caso_eventos", "casos");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Accion).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.DetalleJson).HasColumnType("jsonb");

        builder.HasOne<Caso>()
            .WithMany()
            .HasForeignKey(e => e.CasoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Ejecucion>()
            .WithMany()
            .HasForeignKey(e => e.EjecucionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.CasoId);
        builder.HasIndex(e => e.EjecucionId);
    }
}
