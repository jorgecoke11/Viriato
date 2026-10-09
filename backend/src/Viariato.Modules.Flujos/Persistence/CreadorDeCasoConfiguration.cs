using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Persistence;

public sealed class CreadorDeCasoConfiguration : IEntityTypeConfiguration<CreadorDeCaso>
{
    public void Configure(EntityTypeBuilder<CreadorDeCaso> builder)
    {
        builder.ToTable("creadores_de_caso", "flujos");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Descripcion).HasMaxLength(300);
        builder.Property(c => c.PasoInicialNombre).HasMaxLength(200);
        builder.Property(c => c.PlantillaTitulo).IsRequired().HasMaxLength(300);

        builder.HasOne(c => c.Flujo)
            .WithMany()
            .HasForeignKey(c => c.FlujoId)
            .OnDelete(DeleteBehavior.Cascade);

        // A type or an estado that is retired stays (Activo=false), so these never block anything; if one were ever removed the
        // creator just stops carrying it.
        builder.HasOne(c => c.TipoCaso)
            .WithMany()
            .HasForeignKey(c => c.TipoCasoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.EstadoNegocioInicial)
            .WithMany()
            .HasForeignKey(c => c.EstadoNegocioInicialId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => new { c.FlujoId, c.Nombre }).IsUnique();
    }
}
