using Microsoft.EntityFrameworkCore;

namespace Viariato.Infrastructure;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IEnumerable<IModuleModelConfiguration> moduleConfigurations) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var moduleConfiguration in moduleConfigurations)
        {
            moduleConfiguration.Apply(modelBuilder);
        }

        base.OnModelCreating(modelBuilder);
    }
}
