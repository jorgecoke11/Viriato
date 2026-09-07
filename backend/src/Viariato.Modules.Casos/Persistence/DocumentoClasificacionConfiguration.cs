using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.Persistence;

public sealed class DocumentoClasificacionConfiguration : IEntityTypeConfiguration<DocumentoClasificacion>
{
    public void Configure(EntityTypeBuilder<DocumentoClasificacion> builder)
    {
        builder.ToTable("documento_clasificaciones", "casos");
        builder.HasKey(c => c.Id);

        builder.HasOne<Documento>()
            .WithMany(d => d.Clasificaciones)
            .HasForeignKey(c => c.DocumentoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.TipoDocumento)
            .WithMany()
            .HasForeignKey(c => c.TipoDocumentoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.DocumentoId);
        builder.HasIndex(c => c.TipoDocumentoId);
    }
}
