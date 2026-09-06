using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.Persistence;

public sealed class AgenteEjecucionDetalleConfiguration : IEntityTypeConfiguration<AgenteEjecucionDetalle>
{
    public void Configure(EntityTypeBuilder<AgenteEjecucionDetalle> builder)
    {
        builder.ToTable("agente_ejecucion_detalles", "casos");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Modelo).IsRequired().HasMaxLength(100);
        builder.Property(d => d.DecisionesJson).HasColumnType("jsonb");
        builder.Property(d => d.HerramientasUsadas).HasColumnType("jsonb");
        builder.Property(d => d.InputSnapshot).HasColumnType("jsonb");
        builder.Property(d => d.OutputSnapshot).HasColumnType("jsonb");

        builder.HasOne(d => d.EjecucionPaso)
            .WithMany()
            .HasForeignKey(d => d.EjecucionPasoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.EjecucionPasoId).IsUnique();
    }
}
