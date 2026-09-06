using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;

namespace Viariato.Modules.Flujos.Persistence;

internal sealed class FlujosModuleModelConfiguration : IModuleModelConfiguration
{
    public void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FlujosModuleModelConfiguration).Assembly);
    }
}
