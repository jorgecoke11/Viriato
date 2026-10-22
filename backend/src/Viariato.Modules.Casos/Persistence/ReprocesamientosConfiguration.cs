using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Casos.Persistence;

public sealed class ReglaDeReprocesamientoConfiguration : IEntityTypeConfiguration<ReglaDeReprocesamiento>
{
    public void Configure(EntityTypeBuilder<ReglaDeReprocesamiento> builder)
    {
        builder.ToTable("reglas_de_reprocesamiento", "casos");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(r => r.EstadoEjecucion).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.PasoNombre).HasMaxLength(200);

        builder.HasOne<Flujo>().WithMany().HasForeignKey(r => r.FlujoId).OnDelete(DeleteBehavior.Cascade);
        // A business state in use by a rule cannot be deleted from under it.
        builder.HasOne<FlujoEstadoDef>().WithMany().HasForeignKey(r => r.EstadoNegocioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FlujoEstadoDef>().WithMany().HasForeignKey(r => r.EstadoFinalId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.FlujoId, r.Activa });
    }
}

public sealed class ReprocesamientoProgramadoConfiguration : IEntityTypeConfiguration<ReprocesamientoProgramado>
{
    public void Configure(EntityTypeBuilder<ReprocesamientoProgramado> builder)
    {
        builder.ToTable("reprocesamientos", "casos");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ReglaNombre).HasMaxLength(100).IsRequired();
        builder.Property(r => r.PasoNombre).HasMaxLength(200);
        builder.Property(r => r.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.Error).HasMaxLength(1000);

        builder.HasOne<Flujo>().WithMany().HasForeignKey(r => r.FlujoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ReglaDeReprocesamiento>().WithMany().HasForeignKey(r => r.ReglaId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Caso>().WithMany().HasForeignKey(r => r.CasoId).OnDelete(DeleteBehavior.Cascade);

        // What the reprocessor looks at on every turn, what a rule's limit counts, and the process's list.
        builder.HasIndex(r => new { r.Estado, r.ProgramadoPara });
        builder.HasIndex(r => new { r.CasoId, r.ReglaId });
        builder.HasIndex(r => new { r.FlujoId, r.CreatedAt });
    }
}
