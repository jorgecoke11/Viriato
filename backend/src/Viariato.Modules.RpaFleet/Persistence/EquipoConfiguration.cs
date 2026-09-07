using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.RpaFleet.Persistence;

public sealed class EquipoConfiguration : IEntityTypeConfiguration<Equipo>
{
    public void Configure(EntityTypeBuilder<Equipo> builder)
    {
        builder.ToTable("equipos", "rpafleet");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Nombre).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Descripcion).HasMaxLength(1000);

        builder.HasIndex(e => e.Nombre).IsUnique();
    }
}
