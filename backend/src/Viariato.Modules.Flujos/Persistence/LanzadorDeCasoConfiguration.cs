using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Flujos.Esquemas;

namespace Viariato.Modules.Flujos.Persistence;

public sealed class LanzadorDeCasoConfiguration : IEntityTypeConfiguration<LanzadorDeCaso>
{
    public void Configure(EntityTypeBuilder<LanzadorDeCaso> builder)
    {
        builder.ToTable("lanzadores_de_caso", "flujos");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(l => l.Descripcion).HasMaxLength(300);
        builder.Property(l => l.DatosJson).HasColumnType("jsonb");
        builder.Property(l => l.Cron).IsRequired().HasMaxLength(CronDeLanzador.LongitudMaxima);
        builder.Property(l => l.ZonaHoraria).IsRequired().HasMaxLength(100);
        builder.Property(l => l.UltimoError).HasMaxLength(1000);

        builder.HasOne(l => l.Flujo)
            .WithMany()
            .HasForeignKey(l => l.FlujoId)
            .OnDelete(DeleteBehavior.Cascade);

        // A launcher without its creator means nothing: deleting the creator takes its launchers with it.
        builder.HasOne(l => l.CreadorDeCaso)
            .WithMany()
            .HasForeignKey(l => l.CreadorDeCasoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => new { l.FlujoId, l.Nombre }).IsUnique();
        builder.HasIndex(l => l.CreadorDeCasoId);
    }
}
