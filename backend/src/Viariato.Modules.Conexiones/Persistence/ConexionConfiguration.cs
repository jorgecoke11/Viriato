using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Infrastructure;
using Viariato.Modules.Conexiones.Domain;

namespace Viariato.Modules.Conexiones.Persistence;

public sealed class ConexionConfiguration : IEntityTypeConfiguration<Conexion>
{
    public void Configure(EntityTypeBuilder<Conexion> builder)
    {
        builder.ToTable("conexiones", "conexiones");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Tipo).HasMaxLength(40).IsRequired();
        builder.Property(c => c.ConfiguracionJson).HasColumnType("jsonb").IsRequired();
        builder.Property(c => c.SecretoCifrado).HasMaxLength(4000);

        builder.HasIndex(c => c.Nombre).IsUnique();
    }
}

internal sealed class ConexionesModuleModelConfiguration : IModuleModelConfiguration
{
    public void Apply(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConexionesModuleModelConfiguration).Assembly);
}
