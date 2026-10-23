using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Conexiones.Domain;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Notificaciones.Domain;

namespace Viariato.Modules.Notificaciones.Persistence;

public sealed class ReglaDeNotificacionConfiguration : IEntityTypeConfiguration<ReglaDeNotificacion>
{
    public void Configure(EntityTypeBuilder<ReglaDeNotificacion> builder)
    {
        builder.ToTable("reglas", "notificaciones");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Disparador).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(r => r.Codigo).HasMaxLength(60);
        builder.Property(r => r.Destinatarios).IsRequired(); // text[]
        builder.Property(r => r.Asunto).HasMaxLength(300).IsRequired();
        builder.Property(r => r.Cuerpo).HasMaxLength(5000).IsRequired();

        builder.HasOne<Flujo>().WithMany().HasForeignKey(r => r.FlujoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FlujoTipoCasoDef>().WithMany().HasForeignKey(r => r.TipoCasoId).OnDelete(DeleteBehavior.Cascade);
        // An account in use cannot be deleted from under its rules (the Conexiones screen says so).
        builder.HasOne<Conexion>().WithMany().HasForeignKey(r => r.ConexionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.FlujoId, r.Disparador, r.Activa });
    }
}

public sealed class EnvioDeNotificacionConfiguration : IEntityTypeConfiguration<EnvioDeNotificacion>
{
    public void Configure(EntityTypeBuilder<EnvioDeNotificacion> builder)
    {
        builder.ToTable("envios", "notificaciones");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.ReglaNombre).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Destinatarios).IsRequired(); // text[]
        builder.Property(e => e.Asunto).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Cuerpo).IsRequired();
        builder.Property(e => e.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.UltimoError).HasMaxLength(1000);

        builder.HasOne<ReglaDeNotificacion>().WithMany().HasForeignKey(e => e.ReglaId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<Caso>().WithMany().HasForeignKey(e => e.CasoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Conexion>().WithMany().HasForeignKey(e => e.ConexionId).OnDelete(DeleteBehavior.Restrict);

        // What the postman looks at on every turn.
        builder.HasIndex(e => new { e.Estado, e.ProximoIntentoAt });
        builder.HasIndex(e => new { e.FlujoId, e.CreatedAt });
    }
}

internal sealed class NotificacionesModuleModelConfiguration : IModuleModelConfiguration
{
    public void Apply(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificacionesModuleModelConfiguration).Assembly);
}
