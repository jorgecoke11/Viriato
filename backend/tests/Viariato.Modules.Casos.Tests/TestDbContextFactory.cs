using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Persistence;
using Viariato.Modules.Flujos.Persistence;

namespace Viariato.Modules.Casos.Tests;

internal static class TestDbContextFactory
{
    // Casos references Flujos entities directly (Caso.FlujoVersionId, EjecucionPaso.FlujoPasoDefId),
    // so both modules' real configurations are applied, same as production.
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options, [new FlujosModuleModelConfiguration(), new CasosModuleModelConfiguration()]);
    }
}
