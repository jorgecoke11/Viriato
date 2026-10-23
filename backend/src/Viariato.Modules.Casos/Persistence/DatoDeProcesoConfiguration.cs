using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Casos.Persistence;

public sealed class DatoDeProcesoConfiguration : IEntityTypeConfiguration<DatoDeProceso>
{
    public void Configure(EntityTypeBuilder<DatoDeProceso> builder)
    {
        builder.ToTable("datos_de_proceso", "casos");
        builder.HasKey(d => new { d.FlujoId, d.Clave });

        builder.Property(d => d.Clave).HasMaxLength(DatoDeProceso.LongitudMaximaClave).IsRequired();
        builder.Property(d => d.ValorJson).HasColumnType("jsonb").IsRequired();

        builder.HasOne<Flujo>()
            .WithMany()
            .HasForeignKey(d => d.FlujoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
