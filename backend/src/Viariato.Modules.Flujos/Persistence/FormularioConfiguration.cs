using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Persistence;

public sealed class FormularioConfiguration : IEntityTypeConfiguration<Formulario>
{
    public void Configure(EntityTypeBuilder<Formulario> builder)
    {
        builder.ToTable("formularios", "flujos");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(f => f.Descripcion).HasMaxLength(300);
        builder.Property(f => f.EsquemaJson).IsRequired().HasColumnType("text");

        builder.HasOne(f => f.Flujo)
            .WithMany()
            .HasForeignKey(f => f.FlujoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => new { f.FlujoId, f.Nombre }).IsUnique();
    }
}
