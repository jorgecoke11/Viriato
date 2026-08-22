using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;

namespace Viariato.Modules.Users.Persistence;

internal sealed class UsersModuleModelConfiguration : IModuleModelConfiguration
{
    public void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UsersModuleModelConfiguration).Assembly);
    }
}
