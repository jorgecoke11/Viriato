using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;

namespace Viariato.Modules.Casos.Persistence;

internal sealed class CasosModuleModelConfiguration : IModuleModelConfiguration
{
    public void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CasosModuleModelConfiguration).Assembly);
    }
}
