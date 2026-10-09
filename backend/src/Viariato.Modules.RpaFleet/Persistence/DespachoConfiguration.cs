using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.RpaFleet.Persistence;

public sealed class EquipoServicioOrdenConfiguration : IEntityTypeConfiguration<EquipoServicioOrden>
{
    public void Configure(EntityTypeBuilder<EquipoServicioOrden> builder)
    {
        builder.ToTable("equipo_servicio_orden", "rpafleet");
        builder.HasKey(o => o.Id);

        builder.HasOne(o => o.Equipo).WithMany().HasForeignKey(o => o.EquipoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(o => o.Servicio).WithMany().HasForeignKey(o => o.ServicioId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => new { o.EquipoId, o.ServicioId }).IsUnique();
    }
}

public sealed class PlantillaDespachoConfiguration : IEntityTypeConfiguration<PlantillaDespacho>
{
    public void Configure(EntityTypeBuilder<PlantillaDespacho> builder)
    {
        builder.ToTable("plantillas_despacho", "rpafleet");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nombre).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Descripcion).HasMaxLength(1000);
        builder.Property(p => p.Politica).HasConversion<string>().HasMaxLength(20);

        builder.HasMany(p => p.Servicios).WithOne(s => s.Plantilla).HasForeignKey(s => s.PlantillaId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.Nombre).IsUnique();
    }
}

public sealed class PlantillaDespachoServicioConfiguration : IEntityTypeConfiguration<PlantillaDespachoServicio>
{
    public void Configure(EntityTypeBuilder<PlantillaDespachoServicio> builder)
    {
        builder.ToTable("plantilla_despacho_servicios", "rpafleet");
        builder.HasKey(s => s.Id);

        builder.HasOne(s => s.Servicio).WithMany().HasForeignKey(s => s.ServicioId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.PlantillaId, s.ServicioId }).IsUnique();
    }
}
