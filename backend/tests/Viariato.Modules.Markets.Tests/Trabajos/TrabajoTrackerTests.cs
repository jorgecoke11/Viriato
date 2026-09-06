using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure.Trabajos;

namespace Viariato.Modules.Markets.Tests.Trabajos;

public sealed class TrabajoTrackerTests
{
    [Fact]
    public async Task StartAsync_CreatesRunningTrabajo()
    {
        using var db = TestDbContextFactory.Create();
        var tracker = new TrabajoTracker(db);

        var id = await tracker.StartAsync("test.process", "company", "AAPL");

        var trabajo = await db.Set<Trabajo>().SingleAsync(t => t.Id == id);
        Assert.Equal(TrabajoStatus.Running, trabajo.Status);
        Assert.Equal("test.process", trabajo.ProcessCode);
        Assert.Equal("company", trabajo.SubjectType);
        Assert.Equal("AAPL", trabajo.SubjectKey);
        Assert.Null(trabajo.FinishedAt);
    }

    [Fact]
    public async Task RegistrarLogAsync_AppendsLogLinesForTheTrabajo()
    {
        using var db = TestDbContextFactory.Create();
        var tracker = new TrabajoTracker(db);
        var id = await tracker.StartAsync("test.process");

        await tracker.RegistrarLogAsync(id, "Paso 1 completado");
        await tracker.RegistrarLogAsync(id, "Algo falló", TrabajoLogLevel.Error);

        var logs = await db.Set<TrabajoLog>().Where(e => e.TrabajoId == id).ToListAsync();
        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, e => e.Level == TrabajoLogLevel.Error && e.Message == "Algo falló");
    }

    [Fact]
    public async Task CompleteAsync_MarksCompletedAndSerializesData()
    {
        using var db = TestDbContextFactory.Create();
        var tracker = new TrabajoTracker(db);
        var id = await tracker.StartAsync("test.process");

        await tracker.CompleteAsync(id, data: new { Score = 5 }, summary: "5/7");

        var trabajo = await db.Set<Trabajo>().SingleAsync(t => t.Id == id);
        Assert.Equal(TrabajoStatus.Completed, trabajo.Status);
        Assert.Equal(100, trabajo.Progress);
        Assert.Equal("5/7", trabajo.Summary);
        Assert.NotNull(trabajo.FinishedAt);
        Assert.Contains("\"score\":5", trabajo.Data, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailAsync_MarksFailedWithError()
    {
        using var db = TestDbContextFactory.Create();
        var tracker = new TrabajoTracker(db);
        var id = await tracker.StartAsync("test.process");

        await tracker.FailAsync(id, "Fuente de datos no disponible.");

        var trabajo = await db.Set<Trabajo>().SingleAsync(t => t.Id == id);
        Assert.Equal(TrabajoStatus.Failed, trabajo.Status);
        Assert.Equal("Fuente de datos no disponible.", trabajo.Error);
        Assert.NotNull(trabajo.FinishedAt);
    }

    [Fact]
    public async Task ReportProgressAsync_ClampsToValidRange()
    {
        using var db = TestDbContextFactory.Create();
        var tracker = new TrabajoTracker(db);
        var id = await tracker.StartAsync("test.process");

        await tracker.ReportProgressAsync(id, 150);

        var trabajo = await db.Set<Trabajo>().SingleAsync(t => t.Id == id);
        Assert.Equal(100, trabajo.Progress);
    }

    [Fact]
    public async Task ChildTrabajos_LinkToParentViaParentTrabajoId()
    {
        using var db = TestDbContextFactory.Create();
        var tracker = new TrabajoTracker(db);
        var parentId = await tracker.StartAsync("markets.sync", "index", "Sp500");
        var childId = await tracker.StartAsync("markets.analyze_company", "company", "AAPL", parentId);

        var child = await db.Set<Trabajo>().SingleAsync(t => t.Id == childId);
        Assert.Equal(parentId, child.ParentTrabajoId);
    }
}
