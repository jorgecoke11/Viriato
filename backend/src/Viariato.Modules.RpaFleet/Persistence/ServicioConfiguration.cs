using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.RpaFleet.Persistence;

public sealed class ServicioConfiguration : IEntityTypeConfiguration<Servicio>
{
    public void Configure(EntityTypeBuilder<Servicio> builder)
    {
        builder.ToTable("servicios", "rpafleet");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Nombre).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Descripcion).HasMaxLength(1000);

        builder.HasIndex(s => s.Nombre).IsUnique();
    }
}
