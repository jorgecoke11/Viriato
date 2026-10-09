using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.RpaFleet.Persistence;

public sealed class InstanciaDeDespliegueConfiguration : IEntityTypeConfiguration<InstanciaDeDespliegue>
{
    public void Configure(EntityTypeBuilder<InstanciaDeDespliegue> builder)
    {
        builder.ToTable("despliegue_instancias", "rpafleet");
        builder.HasKey(i => new { i.DespliegueId, i.InstanciaId });

        builder.Property(i => i.InstanciaId).IsRequired().HasMaxLength(InstanciaDeDespliegue.LongitudMaxima);

        builder.HasOne<Despliegue>().WithMany().HasForeignKey(i => i.DespliegueId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.LastSeenAt);
    }
}
