using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Viariato.Infrastructure.Trabajos;

public sealed class TrabajoTracker(AppDbContext db) : ITrabajoTracker
{
    public async Task<Guid> StartAsync(
        string processCode,
        string? subjectType = null,
        string? subjectKey = null,
        Guid? parentTrabajoId = null,
        CancellationToken ct = default)
    {
        var trabajo = new Trabajo
        {
            Id = Guid.NewGuid(),
            ProcessCode = processCode,
            Status = TrabajoStatus.Running,
            SubjectType = subjectType,
            SubjectKey = subjectKey,
            ParentTrabajoId = parentTrabajoId,
            StartedAt = DateTimeOffset.UtcNow,
        };

        db.Add(trabajo);
        await db.SaveChangesAsync(ct);
        return trabajo.Id;
    }

    public async Task RegistrarLogAsync(
        Guid trabajoId,
        string message,
        TrabajoLogLevel level = TrabajoLogLevel.Info,
        CancellationToken ct = default)
    {
        db.Add(new TrabajoLog
        {
            Id = Guid.NewGuid(),
            TrabajoId = trabajoId,
            Timestamp = DateTimeOffset.UtcNow,
            Level = level,
            Message = message,
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task ReportProgressAsync(Guid trabajoId, int progressPercent, CancellationToken ct = default)
    {
        var trabajo = await db.Set<Trabajo>().FirstOrDefaultAsync(t => t.Id == trabajoId, ct);
        if (trabajo is null) return;

        trabajo.Progress = Math.Clamp(progressPercent, 0, 100);
        await db.SaveChangesAsync(ct);
    }

    public async Task CompleteAsync(Guid trabajoId, object? data = null, string? summary = null, CancellationToken ct = default)
    {
        var trabajo = await db.Set<Trabajo>().FirstOrDefaultAsync(t => t.Id == trabajoId, ct);
        if (trabajo is null) return;

        trabajo.Status = TrabajoStatus.Completed;
        trabajo.Progress = 100;
        trabajo.Summary = summary;
        trabajo.FinishedAt = DateTimeOffset.UtcNow;
        if (data is not null) trabajo.Data = JsonSerializer.Serialize(data);

        await db.SaveChangesAsync(ct);
    }

    public async Task FailAsync(Guid trabajoId, string error, object? partialData = null, CancellationToken ct = default)
    {
        var trabajo = await db.Set<Trabajo>().FirstOrDefaultAsync(t => t.Id == trabajoId, ct);
        if (trabajo is null) return;

        trabajo.Status = TrabajoStatus.Failed;
        trabajo.Error = error;
        trabajo.FinishedAt = DateTimeOffset.UtcNow;
        if (partialData is not null) trabajo.Data = JsonSerializer.Serialize(partialData);

        await db.SaveChangesAsync(ct);
    }
}
