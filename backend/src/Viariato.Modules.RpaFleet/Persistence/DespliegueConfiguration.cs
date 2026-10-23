using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.RpaFleet.Persistence;

public sealed class DespliegueConfiguration : IEntityTypeConfiguration<Despliegue>
{
    public void Configure(EntityTypeBuilder<Despliegue> builder)
    {
        builder.ToTable("despliegues", "rpafleet");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.ApiKeyHash).IsRequired().HasMaxLength(200);
        builder.Property(d => d.ApiKeyPrefix).IsRequired().HasMaxLength(20);

        // The database default fills in the Despliegues that existed before; the sentinel (a value no Despliegue has) makes EF always
        // write the real one, or a new Despliegue saved as Apagada (the enum's zero) would silently get the default.
        builder.Property(d => d.VistaEnDirecto).HasConversion<string>().HasMaxLength(20)
            .HasDefaultValue(CalidadVistaEnDirecto.Baja).HasSentinel((CalidadVistaEnDirecto)(-1));
        builder.Property(d => d.GrabacionDeVideo).HasConversion<string>().HasMaxLength(20).HasDefaultValue(GrabacionDeVideo.SegunRobot);

        builder.HasOne(d => d.Equipo)
            .WithMany()
            .HasForeignKey(d => d.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Servicio)
            .WithMany()
            .HasForeignKey(d => d.ServicioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.ApiKeyHash).IsUnique();
        builder.HasIndex(d => new { d.EquipoId, d.ServicioId, d.FlujoId }).IsUnique();
    }
}
