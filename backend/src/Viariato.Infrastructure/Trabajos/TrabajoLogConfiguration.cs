using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Viariato.Infrastructure.Trabajos;

public sealed class TrabajoLogConfiguration : IEntityTypeConfiguration<TrabajoLog>
{
    public void Configure(EntityTypeBuilder<TrabajoLog> builder)
    {
        builder.ToTable("trabajo_logs", "ops");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Level).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.Message).IsRequired().HasMaxLength(1000);

        builder.HasOne(e => e.Trabajo)
            .WithMany()
            .HasForeignKey(e => e.TrabajoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TrabajoId, e.Timestamp });
    }
}
