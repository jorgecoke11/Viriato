using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.Persistence;

public sealed class EvidenciaConfiguration : IEntityTypeConfiguration<Evidencia>
{
    public void Configure(EntityTypeBuilder<Evidencia> builder)
    {
        builder.ToTable("evidencias", "casos");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Tipo).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.Titulo).IsRequired().HasMaxLength(200);
        builder.Property(e => e.ContenidoJson).HasColumnType("jsonb");

        builder.HasOne(e => e.EjecucionPaso)
            .WithMany()
            .HasForeignKey(e => e.EjecucionPasoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Caso>()
            .WithMany()
            .HasForeignKey(e => e.CasoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Documento>()
            .WithMany()
            .HasForeignKey(e => e.DocumentoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.EjecucionPasoId);
        builder.HasIndex(e => e.CasoId);
        builder.HasIndex(e => e.Tipo);
    }
}
