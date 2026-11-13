using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.RpaFleet.Persistence;

public sealed class AjustesDelLanzadorConfiguration : IEntityTypeConfiguration<AjustesDelLanzador>
{
    public void Configure(EntityTypeBuilder<AjustesDelLanzador> builder)
    {
        builder.ToTable("ajustes_del_lanzador", "rpafleet");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
    }
}
