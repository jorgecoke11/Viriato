using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;

namespace Viariato.Modules.Markets.Tests.Trabajos;

internal static class TestDbContextFactory
{
    // Trabajo/TrabajoLog are configured directly in Infrastructure's own assembly (not behind a
    // module's IModuleModelConfiguration), so AppDbContext picks them up with an empty module list.
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options, []);
    }
}
