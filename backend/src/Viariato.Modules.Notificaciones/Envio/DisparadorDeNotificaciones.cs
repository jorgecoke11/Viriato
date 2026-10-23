using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Notificaciones.Domain;

namespace Viariato.Modules.Notificaciones.Envio;

/// <summary>
/// Where rules fire. Everything that happens to a case already leaves a line in its history (CasoEvento) — a business state reached,
/// the case completed or failed, a robot's notice — whoever caused it: a person, a robot, the engine, a launcher. So instead of a call
/// in each of those places, this looks at the history lines about to be saved and queues the notices of the rules that match them,
/// in the same save: a case and its notices are saved together or not at all.
/// </summary>
internal sealed class DisparadorDeNotificaciones(IOptions<NotificacionesOptions> opciones) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } db) await EncolarAsync(db, cancellationToken);
        return result;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } db) EncolarAsync(db, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    private async Task EncolarAsync(DbContext db, CancellationToken ct)
    {
        var hechos = db.ChangeTracker.Entries<CasoEvento>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .Select(e => (Evento: e, Hecho: QueDispara(e)))
            .Where(h => h.Hecho is not null)
            .ToList();
        if (hechos.Count == 0) return;

        foreach (var (evento, hecho) in hechos)
        {
            var (disparador, codigo, eventoJson) = hecho!.Value;

            // The case may be one being created in this very save.
            var caso = db.ChangeTracker.Entries<Caso>().Select(e => e.Entity).FirstOrDefault(c => c.Id == evento.CasoId)
                ?? await db.Set<Caso>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == evento.CasoId, ct);
            if (caso is null) continue;

            var reglas = await db.Set<ReglaDeNotificacion>().AsNoTracking()
                .Where(r => r.Activa && r.FlujoId == caso.FlujoId && r.Disparador == disparador
                    && (r.TipoCasoId == null || r.TipoCasoId == caso.TipoCasoId)
                    && (codigo == null || r.Codigo == codigo))
                .ToListAsync(ct);
            if (reglas.Count == 0) continue;

            var contexto = await ContextoAsync(db, caso, codigo, eventoJson, evento.OccurredAt, ct);
            foreach (var regla in reglas)
            {
                var (asunto, cuerpo) = RedaccionDeAvisos.Redactar(regla.Asunto, regla.Cuerpo, contexto, opciones.Value);
                db.Add(new EnvioDeNotificacion
                {
                    FlujoId = caso.FlujoId,
                    ReglaId = regla.Id,
                    ReglaNombre = regla.Nombre,
                    CasoId = caso.Id,
                    ConexionId = regla.ConexionId,
                    Destinatarios = [.. regla.Destinatarios],
                    Asunto = asunto,
                    Cuerpo = cuerpo,
                    CreatedAt = DateTimeOffset.UtcNow,
                    ProximoIntentoAt = DateTimeOffset.UtcNow,
                });
            }
        }
    }

    /// <summary>Which trigger a history line is, with its code (the state's or the robot's event) and the robot's data; null if none.</summary>
    private static (DisparadorDeNotificacion, string?, string?)? QueDispara(CasoEvento evento)
    {
        switch (evento.Accion)
        {
            case CasoEventoAccion.Completado:
                return (DisparadorDeNotificacion.CasoCompletado, null, null);
            case CasoEventoAccion.Fallido:
                return (DisparadorDeNotificacion.CasoFallido, null, null);
            case CasoEventoAccion.EstadoNegocioActualizado when Leer(evento.DetalleJson, "Codigo") is { ValueKind: JsonValueKind.String } codigo:
                return (DisparadorDeNotificacion.EstadoNegocio, codigo.GetString(), null);
            case CasoEventoAccion.AvisoDelRobot when Leer(evento.DetalleJson, "evento") is { ValueKind: JsonValueKind.String } nombre:
                var datos = Leer(evento.DetalleJson, "datos");
                return (DisparadorDeNotificacion.EventoDelRobot, nombre.GetString(), datos is { ValueKind: JsonValueKind.Object } d ? d.GetRawText() : null);
            default:
                return null;
        }
    }

    private static JsonElement? Leer(string? json, string propiedad)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var documento = JsonDocument.Parse(json);
            return documento.RootElement.ValueKind == JsonValueKind.Object && documento.RootElement.TryGetProperty(propiedad, out var valor) ? valor.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<ContextoDelAviso> ContextoAsync(
        DbContext db, Caso caso, string? codigo, string? eventoJson, DateTimeOffset cuando, CancellationToken ct)
    {
        var proceso = await db.Set<Flujo>().AsNoTracking().Where(f => f.Id == caso.FlujoId).Select(f => f.Nombre).FirstOrDefaultAsync(ct) ?? string.Empty;
        var tipo = caso.TipoCasoId is { } tipoId
            ? await db.Set<FlujoTipoCasoDef>().AsNoTracking().Where(t => t.Id == tipoId).Select(t => t.Nombre).FirstOrDefaultAsync(ct)
            : null;
        var estadoId = caso.EstadoNegocioActualId;
        var estado = estadoId is { } id
            ? await db.Set<FlujoEstadoDef>().AsNoTracking().Where(e => e.Id == id).Select(e => e.Display).FirstOrDefaultAsync(ct)
            : null;
        return new ContextoDelAviso(caso.Id, caso.Titulo, proceso, tipo, estado ?? codigo, cuando, caso.DatosJson, eventoJson);
    }
}
