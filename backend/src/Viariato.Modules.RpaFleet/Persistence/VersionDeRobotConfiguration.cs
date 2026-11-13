using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.RpaFleet.Persistence;

public sealed class VersionDeRobotConfiguration : IEntityTypeConfiguration<VersionDeRobot>
{
    public void Configure(EntityTypeBuilder<VersionDeRobot> builder)
    {
        builder.ToTable("versiones_de_robot", "rpafleet");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.NombreArchivo).IsRequired().HasMaxLength(260);
        builder.Property(v => v.Sha256).IsRequired().HasMaxLength(64);
        builder.Property(v => v.Ruta).IsRequired().HasMaxLength(500);
        builder.Property(v => v.Raiz).IsRequired().HasMaxLength(260);
        builder.Property(v => v.Comando).IsRequired().HasMaxLength(260);
        builder.Property(v => v.EntornoJson).HasColumnType("jsonb");
        builder.Property(v => v.SubidaPor).HasMaxLength(256);

        // A service with versions cannot be removed (the endpoint refuses), so its zips are never orphaned on disk.
        builder.HasOne<Servicio>()
            .WithMany(s => s.Versiones)
            .HasForeignKey(v => v.ServicioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => new { v.ServicioId, v.Numero }).IsUnique();
    }
}
