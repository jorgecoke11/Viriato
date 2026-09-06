using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Persistence;

public sealed class AgenteDefinicionConfiguration : IEntityTypeConfiguration<AgenteDefinicion>
{
    public void Configure(EntityTypeBuilder<AgenteDefinicion> builder)
    {
        builder.ToTable("agente_definiciones", "flujos");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Nombre).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Descripcion).HasMaxLength(1000);
        builder.Property(a => a.Modelo).IsRequired().HasMaxLength(100);
        builder.Property(a => a.SystemPrompt).IsRequired().HasColumnType("text");
        builder.Property(a => a.HerramientasPermitidas).HasColumnType("jsonb");
        builder.Property(a => a.ParametrosJson).HasColumnType("jsonb");

        builder.HasIndex(a => a.Nombre);
    }
}
