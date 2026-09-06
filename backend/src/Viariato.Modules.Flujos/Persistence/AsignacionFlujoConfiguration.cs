using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Persistence;

public sealed class AsignacionFlujoConfiguration : IEntityTypeConfiguration<AsignacionFlujo>
{
    public void Configure(EntityTypeBuilder<AsignacionFlujo> builder)
    {
        builder.ToTable("asignaciones_flujo", "flujos");
        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.Flujo)
            .WithMany()
            .HasForeignKey(a => a.FlujoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.FlujoId, a.UserId }).IsUnique();
        builder.HasIndex(a => a.UserId);
    }
}
