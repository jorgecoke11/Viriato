using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Users.Persistence;

namespace Viariato.Modules.Users.Tests;

internal static class TestDbContextFactory
{
    // Applies the real UsersModuleModelConfiguration (schema, keys, unique indexes, FK behavior)
    // so unit tests exercise the same EF model shape as production, just against InMemory storage.
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options, [new UsersModuleModelConfiguration()]);
    }
}
