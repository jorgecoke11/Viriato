using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Persistence;

public sealed class FlujoTransicionDefConfiguration : IEntityTypeConfiguration<FlujoTransicionDef>
{
    public void Configure(EntityTypeBuilder<FlujoTransicionDef> builder)
    {
        builder.ToTable("flujo_transicion_defs", "flujos");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Resultado).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne(t => t.FlujoVersion)
            .WithMany(v => v.Transiciones)
            .HasForeignKey(t => t.FlujoVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.PasoOrigen)
            .WithMany()
            .HasForeignKey(t => t.PasoOrigenId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(t => t.PasoDestino)
            .WithMany()
            .HasForeignKey(t => t.PasoDestinoId)
            .OnDelete(DeleteBehavior.Cascade);

        // A state an arrow names cannot disappear under it (states are switched off, not deleted).
        builder.HasOne(t => t.EstadoNegocio)
            .WithMany()
            .HasForeignKey(t => t.EstadoNegocioId)
            .OnDelete(DeleteBehavior.Restrict);

        // One destination per (origin, result, state), "any state" and the start arrow included: nulls count as a value.
        builder.HasIndex(t => new { t.FlujoVersionId, t.PasoOrigenId, t.Resultado, t.EstadoNegocioId })
            .IsUnique()
            .AreNullsDistinct(false);
        builder.HasIndex(t => t.PasoOrigenId);
        builder.HasIndex(t => t.PasoDestinoId);
        builder.HasIndex(t => t.EstadoNegocioId);
    }
}
