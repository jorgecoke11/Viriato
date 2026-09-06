using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Persistence;

public sealed class FlujoPasoDefConfiguration : IEntityTypeConfiguration<FlujoPasoDef>
{
    public void Configure(EntityTypeBuilder<FlujoPasoDef> builder)
    {
        builder.ToTable("flujo_paso_defs", "flujos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nombre).IsRequired().HasMaxLength(200);
        builder.Property(p => p.TipoPaso).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(p => p.ConfiguracionJson).HasColumnType("jsonb");

        builder.HasOne(p => p.FlujoVersion)
            .WithMany(v => v.Pasos)
            .HasForeignKey(p => p.FlujoVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.AgenteDefinicion)
            .WithMany()
            .HasForeignKey(p => p.AgenteDefinicionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.FlujoVersionId, p.Orden }).IsUnique();
        builder.HasIndex(p => p.FlujoVersionId);
        builder.HasIndex(p => p.TipoPaso);
        builder.HasIndex(p => p.AgenteDefinicionId);
    }
}
