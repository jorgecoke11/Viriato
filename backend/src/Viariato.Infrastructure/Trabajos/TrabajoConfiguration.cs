using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Viariato.Infrastructure.Trabajos;

public sealed class TrabajoConfiguration : IEntityTypeConfiguration<Trabajo>
{
    public void Configure(EntityTypeBuilder<Trabajo> builder)
    {
        builder.ToTable("trabajos", "ops");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.ProcessCode).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.SubjectType).HasMaxLength(50);
        builder.Property(t => t.SubjectKey).HasMaxLength(100);
        builder.Property(t => t.Summary).HasMaxLength(500);
        builder.Property(t => t.Data).HasColumnType("jsonb");
        builder.Property(t => t.Error).HasColumnType("text");

        builder.HasOne(t => t.ParentTrabajo)
            .WithMany()
            .HasForeignKey(t => t.ParentTrabajoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.ProcessCode);
        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => new { t.SubjectType, t.SubjectKey });
        builder.HasIndex(t => t.ParentTrabajoId);
        builder.HasIndex(t => t.Data).HasMethod("gin");
    }
}
