using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Infrastructure.Trabajos;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Casos.Persistence;

public sealed class EjecucionPasoConfiguration : IEntityTypeConfiguration<EjecucionPaso>
{
    public void Configure(EntityTypeBuilder<EjecucionPaso> builder)
    {
        builder.ToTable("ejecucion_pasos", "casos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.TipoPaso).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(p => p.Estado).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(p => p.ErrorMensaje).HasColumnType("text");

        builder.HasOne(p => p.Ejecucion)
            .WithMany(e => e.Pasos)
            .HasForeignKey(p => p.EjecucionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Caso>()
            .WithMany()
            .HasForeignKey(p => p.CasoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.FlujoPasoDef)
            .WithMany()
            .HasForeignKey(p => p.FlujoPasoDefId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Trabajo>()
            .WithMany()
            .HasForeignKey(p => p.TrabajoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.EjecucionId, p.FlujoPasoDefId, p.NumeroIntento }).IsUnique();
        builder.HasIndex(p => new { p.EjecucionId, p.FlujoPasoDefId });
        builder.HasIndex(p => p.CasoId);
        builder.HasIndex(p => p.Estado);
        builder.HasIndex(p => p.TrabajoId);
    }
}
