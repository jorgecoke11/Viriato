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

        // Its versions go with the Despliegue.
        builder.HasOne<Despliegue>()
            .WithMany()
            .HasForeignKey(v => v.DespliegueId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => new { v.DespliegueId, v.Numero }).IsUnique();
    }
}
