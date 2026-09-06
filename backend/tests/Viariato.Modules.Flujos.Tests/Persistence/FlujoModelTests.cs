using Microsoft.EntityFrameworkCore;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Flujos.Tests.Persistence;

public sealed class FlujoModelTests
{
    [Fact]
    public async Task SavingAFlujo_PersistsVersionesAndPasosTogether()
    {
        using var db = TestDbContextFactory.Create();

        var flujo = new Flujo { Nombre = "Alta de cliente", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var version = new FlujoVersion { Flujo = flujo, NumeroVersion = 1, CreatedAt = DateTimeOffset.UtcNow };
        version.Pasos.Add(new FlujoPasoDef { FlujoVersion = version, Orden = 1, Nombre = "Descargar", TipoPaso = TipoPaso.Rpa, CreatedAt = DateTimeOffset.UtcNow });
        flujo.Versiones.Add(version);

        db.Add(flujo);
        await db.SaveChangesAsync();

        var reloaded = await db.Set<Flujo>()
            .Include(f => f.Versiones).ThenInclude(v => v.Pasos)
            .SingleAsync(f => f.Id == flujo.Id);

        Assert.Single(reloaded.Versiones);
        Assert.Single(reloaded.Versiones.Single().Pasos);
        Assert.Equal(TipoPaso.Rpa, reloaded.Versiones.Single().Pasos.Single().TipoPaso);
    }

    [Fact]
    public async Task PublishingAVersion_SetsFlujoVersionActiva()
    {
        using var db = TestDbContextFactory.Create();

        var flujo = new Flujo { Nombre = "Alta de expediente", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var version = new FlujoVersion { Flujo = flujo, NumeroVersion = 1, CreatedAt = DateTimeOffset.UtcNow };
        db.Add(flujo);
        db.Add(version);
        await db.SaveChangesAsync();

        version.Estado = FlujoVersionEstado.Publicada;
        version.PublishedAt = DateTimeOffset.UtcNow;
        flujo.VersionActivaId = version.Id;
        await db.SaveChangesAsync();

        var reloaded = await db.Set<Flujo>().Include(f => f.VersionActiva).SingleAsync(f => f.Id == flujo.Id);
        Assert.Equal(version.Id, reloaded.VersionActivaId);
        Assert.Equal(FlujoVersionEstado.Publicada, reloaded.VersionActiva!.Estado);
    }
}
