using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.RpaFleet.Persistence;

public sealed class CredencialConfiguration : IEntityTypeConfiguration<Credencial>
{
    public void Configure(EntityTypeBuilder<Credencial> builder)
    {
        builder.ToTable("credenciales", "rpafleet");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Descripcion).HasMaxLength(1000);
        builder.Property(c => c.Usuario).HasMaxLength(200);
        builder.Property(c => c.PasswordCifrado).IsRequired().HasMaxLength(4000);

        builder.HasOne(c => c.Servicio)
            .WithMany()
            .HasForeignKey(c => c.ServicioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.Nombre).IsUnique();
        builder.HasIndex(c => c.ServicioId);
    }
}
