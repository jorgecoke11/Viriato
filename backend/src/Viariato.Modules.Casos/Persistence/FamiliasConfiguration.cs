using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Casos.Persistence;

public sealed class TipoDeFamiliaConfiguration : IEntityTypeConfiguration<TipoDeFamilia>
{
    public void Configure(EntityTypeBuilder<TipoDeFamilia> builder)
    {
        builder.ToTable("tipos_de_familia", "casos");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Descripcion).HasMaxLength(500);
        builder.Property(t => t.Condicion).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne<Flujo>().WithMany().HasForeignKey(t => t.FlujoId).OnDelete(DeleteBehavior.Cascade);
        // A creator in use as a closing action cannot be deleted from under it.
        builder.HasOne<CreadorDeCaso>().WithMany().HasForeignKey(t => t.CreadorAlTerminarId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.FlujoId, t.Nombre }).IsUnique();
    }
}

public sealed class FamiliaConfiguration : IEntityTypeConfiguration<Familia>
{
    public void Configure(EntityTypeBuilder<Familia> builder)
    {
        builder.ToTable("familias", "casos");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Nombre).HasMaxLength(200).IsRequired();
        builder.Property(f => f.Estado).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(f => f.NotaDeCierre).HasMaxLength(1000);

        builder.HasOne<Flujo>().WithMany().HasForeignKey(f => f.FlujoId).OnDelete(DeleteBehavior.Cascade);
        // A type with families cannot be deleted (deactivate it instead): their history would lose what they were.
        builder.HasOne(f => f.Tipo).WithMany().HasForeignKey(f => f.TipoDeFamiliaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Caso>().WithMany().HasForeignKey(f => f.CasoDeCierreId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(f => f.Miembros).WithOne().HasForeignKey(m => m.FamiliaId).OnDelete(DeleteBehavior.Cascade);

        // What the finisher looks at on every turn (only the open and closed ones), and the process's list.
        builder.HasIndex(f => f.Estado).HasFilter("estado <> 'Terminada'");
        builder.HasIndex(f => new { f.FlujoId, f.CreatedAt });
    }
}

public sealed class MiembroDeFamiliaConfiguration : IEntityTypeConfiguration<MiembroDeFamilia>
{
    public void Configure(EntityTypeBuilder<MiembroDeFamilia> builder)
    {
        builder.ToTable("familia_miembros", "casos");
        builder.HasKey(m => new { m.FamiliaId, m.CasoId });

        builder.HasOne<Caso>().WithMany().HasForeignKey(m => m.CasoId).OnDelete(DeleteBehavior.Cascade);

        // A case's families (its detail, the cases list's filter goes by the key).
        builder.HasIndex(m => m.CasoId);
    }
}

public sealed class FamiliaEventoConfiguration : IEntityTypeConfiguration<FamiliaEvento>
{
    public void Configure(EntityTypeBuilder<FamiliaEvento> builder)
    {
        builder.ToTable("familia_eventos", "casos");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Accion).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(e => e.DetalleJson).HasColumnType("jsonb");

        builder.HasOne<Familia>().WithMany().HasForeignKey(e => e.FamiliaId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.FamiliaId, e.OccurredAt });
    }
}
