using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Casos.Persistence;

public sealed class CasoConfiguration : IEntityTypeConfiguration<Caso>
{
    public void Configure(EntityTypeBuilder<Caso> builder)
    {
        builder.ToTable("casos", "casos");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Titulo).IsRequired().HasMaxLength(300);
        builder.Property(c => c.Estado).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(c => c.DatosJson).HasColumnType("jsonb");

        builder.HasOne<Flujo>()
            .WithMany()
            .HasForeignKey(c => c.FlujoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<FlujoVersion>()
            .WithMany()
            .HasForeignKey(c => c.FlujoVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.EjecucionActual)
            .WithMany()
            .HasForeignKey(c => c.EjecucionActualId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.EstadoNegocioActual)
            .WithMany()
            .HasForeignKey(c => c.EstadoNegocioActualId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.TipoCaso)
            .WithMany()
            .HasForeignKey(c => c.TipoCasoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.FlujoId);
        builder.HasIndex(c => c.FlujoVersionId);
        builder.HasIndex(c => c.Estado);
        builder.HasIndex(c => c.EstadoNegocioActualId);
        builder.HasIndex(c => c.TipoCasoId);
        builder.HasIndex(c => c.DatosJson).HasMethod("gin");
    }
}
