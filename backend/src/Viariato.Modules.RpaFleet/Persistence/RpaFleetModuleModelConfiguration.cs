using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;

namespace Viariato.Modules.RpaFleet.Persistence;

internal sealed class RpaFleetModuleModelConfiguration : IModuleModelConfiguration
{
    public void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RpaFleetModuleModelConfiguration).Assembly);
    }
}
