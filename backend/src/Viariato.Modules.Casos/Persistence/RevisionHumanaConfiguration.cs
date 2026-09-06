using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.Persistence;

public sealed class RevisionHumanaConfiguration : IEntityTypeConfiguration<RevisionHumana>
{
    public void Configure(EntityTypeBuilder<RevisionHumana> builder)
    {
        builder.ToTable("revisiones_humanas", "casos");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Decision).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.Comentario).HasMaxLength(2000);

        builder.HasOne(r => r.EjecucionPaso)
            .WithMany()
            .HasForeignKey(r => r.EjecucionPasoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.EjecucionPasoId).IsUnique();
    }
}
