using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Casos.Persistence;

public sealed class EjecucionConfiguration : IEntityTypeConfiguration<Ejecucion>
{
    public void Configure(EntityTypeBuilder<Ejecucion> builder)
    {
        builder.ToTable("ejecuciones", "casos");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Estado).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasOne(e => e.Caso)
            .WithMany()
            .HasForeignKey(e => e.CasoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<FlujoVersion>()
            .WithMany()
            .HasForeignKey(e => e.FlujoVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PasoActual)
            .WithMany()
            .HasForeignKey(e => e.PasoActualId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Pasos)
            .WithOne(p => p.Ejecucion)
            .HasForeignKey(p => p.EjecucionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.CasoId);
        builder.HasIndex(e => e.Estado);
        builder.HasIndex(e => e.PasoActualId);
    }
}
