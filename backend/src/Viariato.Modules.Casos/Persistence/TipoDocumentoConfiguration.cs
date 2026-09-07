using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.Persistence;

public sealed class TipoDocumentoConfiguration : IEntityTypeConfiguration<TipoDocumento>
{
    public void Configure(EntityTypeBuilder<TipoDocumento> builder)
    {
        builder.ToTable("tipos_documento", "casos");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Nombre).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Descripcion).HasMaxLength(1000);

        builder.HasIndex(t => t.Nombre).IsUnique();
    }
}
