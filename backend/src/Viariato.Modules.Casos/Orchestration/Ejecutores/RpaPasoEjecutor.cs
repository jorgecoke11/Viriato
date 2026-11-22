using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Infrastructure.Trabajos;
using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.Orchestration.Ejecutores;

/// <summary>
/// Starts a Trabajo and creates the RpaEjecucionDetalle a real robot will pick up, then genuinely
/// waits: PasoResultado.EnProgreso here means "parked until an external worker calls
/// /api/v1/rpa/pasos/{id}/completar or /fallar" (see Endpoints/RpaWorkerEndpoints.cs), not "I'll
/// finish this myself in a moment" — there is no simulated robot anymore.
/// </summary>
public sealed class RpaPasoEjecutor(AppDbContext db, ITrabajoTracker tracker) : IPasoEjecutor
{
    public async Task<PasoResultado> EjecutarAsync(PasoEjecucionContext context, CancellationToken ct)
    {
        // A step of the arrow-shaped flow has no configuration: the application robots are told about is its servicio's name.
        var aplicacion = ExtraerAplicacion(context.PasoDef.ConfiguracionJson)
            ?? (context.PasoDef.ServicioId is { } servicioId
                ? await db.Set<RpaFleet.Domain.Servicio>().AsNoTracking().Where(s => s.Id == servicioId).Select(s => s.Nombre).FirstOrDefaultAsync(ct)
                : null)
            ?? "desconocida";

        var trabajoId = await tracker.StartAsync("casos.paso.rpa", "ejecucion_paso", context.EjecucionPasoId.ToString(), ct: ct);

        var paso = await db.Set<EjecucionPaso>().FirstAsync(p => p.Id == context.EjecucionPasoId, ct);
        paso.TrabajoId = trabajoId;
        db.Add(new RpaEjecucionDetalle
        {
            EjecucionPasoId = paso.Id,
            AplicacionObjetivo = aplicacion,
            ParametrosEntrada = context.PasoDef.ConfiguracionJson,
            Prioridad = context.Prioridad,
        });
        await db.SaveChangesAsync(ct);

        return PasoResultado.EnProgreso;
    }

    private static string? ExtraerAplicacion(string? configuracionJson)
    {
        if (string.IsNullOrWhiteSpace(configuracionJson)) return null;
        using var doc = JsonDocument.Parse(configuracionJson);
        return doc.RootElement.TryGetProperty("aplicacion", out var valor) ? valor.GetString() : null;
    }
}
