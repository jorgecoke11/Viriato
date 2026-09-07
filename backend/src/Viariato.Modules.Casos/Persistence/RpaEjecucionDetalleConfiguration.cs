using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.Persistence;

public sealed class RpaEjecucionDetalleConfiguration : IEntityTypeConfiguration<RpaEjecucionDetalle>
{
    public void Configure(EntityTypeBuilder<RpaEjecucionDetalle> builder)
    {
        builder.ToTable("rpa_ejecucion_detalles", "casos");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.AplicacionObjetivo).IsRequired().HasMaxLength(200);
        builder.Property(d => d.WorkerId).HasMaxLength(100);
        builder.Property(d => d.ParametrosEntrada).HasColumnType("jsonb");
        builder.Property(d => d.ParametrosSalida).HasColumnType("jsonb");

        builder.HasOne(d => d.EjecucionPaso)
            .WithMany()
            .HasForeignKey(d => d.EjecucionPasoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.EjecucionPasoId).IsUnique();
        builder.HasIndex(d => d.DespliegueId);
    }
}
