using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.Persistence;

public sealed class DocumentoConfiguration : IEntityTypeConfiguration<Documento>
{
    public void Configure(EntityTypeBuilder<Documento> builder)
    {
        builder.ToTable("documentos", "casos");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Nombre).IsRequired().HasMaxLength(300);
        builder.Property(d => d.ContentType).IsRequired().HasMaxLength(150);
        builder.Property(d => d.StorageKey).IsRequired().HasMaxLength(500);
        builder.Property(d => d.Hash).HasMaxLength(100);

        builder.HasOne<Caso>()
            .WithMany()
            .HasForeignKey(d => d.CasoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<EjecucionPaso>()
            .WithMany()
            .HasForeignKey(d => d.EjecucionPasoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(d => d.CasoId);
        builder.HasIndex(d => d.EjecucionPasoId);
    }
}
