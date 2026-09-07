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
