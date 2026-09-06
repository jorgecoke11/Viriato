using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Casos.Orchestration;

/// <summary>
/// Decides what runs next for a Caso's Ejecucion. Workers (RPA/Agent/API executors) never know about
/// the Caso or the Flujo — they only receive one step's input and report back success/failure; this
/// class is the only place that reads the FlujoVersion definition and moves the Ejecucion forward.
/// Every transition writes a <see cref="CasoEvento"/> inline, never as a hidden side effect, so the
/// full history is always reconstructable and a future real-time push has a clean hook.
/// </summary>
public sealed class EjecucionOrchestrator(AppDbContext db, IServiceProvider services) : IEjecucionOrchestrator
{
    public async Task<Guid> IniciarCasoAsync(Guid casoId, Guid? pasoInicialId, CancellationToken ct)
    {
        var caso = await db.Set<Caso>().FirstOrDefaultAsync(c => c.Id == casoId, ct)
            ?? throw new InvalidOperationException($"Caso {casoId} no encontrado.");

        var pasos = await db.Set<FlujoPasoDef>().AsNoTracking()
            .Where(p => p.FlujoVersionId == caso.FlujoVersionId)
            .OrderBy(p => p.Orden)
            .ToListAsync(ct);
        if (pasos.Count == 0)
        {
            throw new InvalidOperationException("La FlujoVersion no tiene pasos.");
        }

        var pasoInicial = pasoInicialId is null
            ? pasos[0]
            : pasos.FirstOrDefault(p => p.Id == pasoInicialId)
                ?? throw new InvalidOperationException("El paso inicial indicado no pertenece a esta FlujoVersion.");

        var ejecucion = new Ejecucion
        {
            CasoId = casoId,
            FlujoVersionId = caso.FlujoVersionId,
            Estado = EjecucionEstado.EnProgreso,
            StartedAt = DateTimeOffset.UtcNow,
        };
        db.Add(ejecucion);
        await db.SaveChangesAsync(ct);

        caso.EjecucionActualId = ejecucion.Id;
        caso.Estado = CasoEstado.EnProgreso;
        caso.UpdatedAt = DateTimeOffset.UtcNow;
        await RegistrarEventoAsync(casoId, ejecucion.Id, CasoEventoAccion.Iniciado, null, ct);

        // Starting past the first step (e.g. "the documentation was already validated externally") —
        // every step before it is recorded as Omitido for traceability, the same mechanism a Decision
        // branch uses for the side not taken, rather than just silently never mentioning them.
        foreach (var pasoAnterior in pasos.Where(p => p.Orden < pasoInicial.Orden))
        {
            db.Add(new EjecucionPaso
            {
                EjecucionId = ejecucion.Id,
                CasoId = casoId,
                FlujoPasoDefId = pasoAnterior.Id,
                TipoPaso = pasoAnterior.TipoPaso,
                NumeroIntento = 1,
                Estado = EjecucionPasoEstado.Omitido,
                CreatedAt = DateTimeOffset.UtcNow,
                FinishedAt = DateTimeOffset.UtcNow,
            });
        }
        await db.SaveChangesAsync(ct);

        await CrearYDespacharPasoAsync(ejecucion, caso, pasoInicial, numeroIntento: 1, ct);

        return ejecucion.Id;
    }

    public async Task AvanzarAsync(Guid ejecucionPasoId, CancellationToken ct)
    {
        var paso = await db.Set<EjecucionPaso>().FirstOrDefaultAsync(p => p.Id == ejecucionPasoId, ct)
            ?? throw new InvalidOperationException($"EjecucionPaso {ejecucionPasoId} no encontrado.");
        var ejecucion = await db.Set<Ejecucion>().FirstOrDefaultAsync(e => e.Id == paso.EjecucionId, ct)
            ?? throw new InvalidOperationException($"Ejecucion {paso.EjecucionId} no encontrada.");
        var caso = await db.Set<Caso>().FirstOrDefaultAsync(c => c.Id == ejecucion.CasoId, ct)
            ?? throw new InvalidOperationException($"Caso {ejecucion.CasoId} no encontrado.");

        switch (paso.Estado)
        {
            case EjecucionPasoEstado.Completado:
                await RegistrarEventoAsync(caso.Id, ejecucion.Id, CasoEventoAccion.PasoCompletado, paso.Id, ct);
                await AplicarEstadoNegocioSiCorrespondeAsync(caso, paso, ct);
                await ContinuarTrasCompletarAsync(ejecucion, caso, paso, ct);
                break;

            case EjecucionPasoEstado.Fallido:
                await RegistrarEventoAsync(caso.Id, ejecucion.Id, CasoEventoAccion.PasoFallido, paso.Id, ct);
                await FinalizarAsync(ejecucion, caso, EjecucionEstado.Fallida, CasoEstado.Fallido, CasoEventoAccion.Fallido, ct);
                break;

            case EjecucionPasoEstado.EsperandoRevisionHumana:
                await RegistrarEventoAsync(caso.Id, ejecucion.Id, CasoEventoAccion.RevisionSolicitada, paso.Id, ct);
                ejecucion.Estado = EjecucionEstado.EsperandoRevisionHumana;
                caso.Estado = CasoEstado.EsperandoRevisionHumana;
                caso.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);
                break;

            default:
                // EnProgreso/Pendiente/Cancelado/Omitido: nothing to advance from here yet.
                break;
        }
    }

    public async Task ReintentarPasoAsync(Guid ejecucionPasoId, CancellationToken ct)
    {
        var pasoFallido = await db.Set<EjecucionPaso>().FirstOrDefaultAsync(p => p.Id == ejecucionPasoId, ct)
            ?? throw new InvalidOperationException($"EjecucionPaso {ejecucionPasoId} no encontrado.");

        if (pasoFallido.Estado != EjecucionPasoEstado.Fallido)
        {
            throw new InvalidOperationException("Solo se puede reintentar un paso fallido.");
        }

        var hayIntentoPosterior = await db.Set<EjecucionPaso>().AnyAsync(p =>
            p.EjecucionId == pasoFallido.EjecucionId
            && p.FlujoPasoDefId == pasoFallido.FlujoPasoDefId
            && p.NumeroIntento > pasoFallido.NumeroIntento, ct);
        if (hayIntentoPosterior)
        {
            throw new InvalidOperationException("Solo se puede reintentar el último intento de un paso.");
        }

        var ejecucion = await db.Set<Ejecucion>().FirstAsync(e => e.Id == pasoFallido.EjecucionId, ct);
        var caso = await db.Set<Caso>().FirstAsync(c => c.Id == ejecucion.CasoId, ct);
        var pasoDef = await db.Set<FlujoPasoDef>().AsNoTracking().FirstAsync(p => p.Id == pasoFallido.FlujoPasoDefId, ct);

        ejecucion.Estado = EjecucionEstado.EnProgreso;
        caso.Estado = CasoEstado.EnProgreso;
        caso.UpdatedAt = DateTimeOffset.UtcNow;
        await RegistrarEventoAsync(caso.Id, ejecucion.Id, CasoEventoAccion.PasoReintentado, pasoFallido.Id, ct);

        await CrearYDespacharPasoAsync(ejecucion, caso, pasoDef, pasoFallido.NumeroIntento + 1, ct);
    }

    public async Task PausarAsync(Guid casoId, CancellationToken ct)
    {
        var caso = await db.Set<Caso>().FirstOrDefaultAsync(c => c.Id == casoId, ct)
            ?? throw new InvalidOperationException($"Caso {casoId} no encontrado.");
        if (caso.EjecucionActualId is null)
        {
            throw new InvalidOperationException("El caso no tiene una ejecución en curso.");
        }

        var ejecucion = await db.Set<Ejecucion>().FirstAsync(e => e.Id == caso.EjecucionActualId, ct);

        caso.Estado = CasoEstado.Pausado;
        caso.UpdatedAt = DateTimeOffset.UtcNow;
        ejecucion.Estado = EjecucionEstado.Pausada;

        await RegistrarEventoAsync(casoId, ejecucion.Id, CasoEventoAccion.Pausado, ejecucion.PasoActualId, ct);
    }

    public async Task ReanudarAsync(Guid casoId, CancellationToken ct)
    {
        var caso = await db.Set<Caso>().FirstOrDefaultAsync(c => c.Id == casoId, ct)
            ?? throw new InvalidOperationException($"Caso {casoId} no encontrado.");
        if (caso.EjecucionActualId is null)
        {
            throw new InvalidOperationException("El caso no tiene una ejecución en curso.");
        }

        var ejecucion = await db.Set<Ejecucion>().FirstAsync(e => e.Id == caso.EjecucionActualId, ct);
        if (ejecucion.Estado != EjecucionEstado.Pausada)
        {
            throw new InvalidOperationException("Solo se puede reanudar una ejecución pausada.");
        }

        caso.Estado = CasoEstado.EnProgreso;
        caso.UpdatedAt = DateTimeOffset.UtcNow;
        ejecucion.Estado = EjecucionEstado.EnProgreso;
        await RegistrarEventoAsync(casoId, ejecucion.Id, CasoEventoAccion.Reanudado, ejecucion.PasoActualId, ct);

        if (ejecucion.PasoActualId is not { } pasoActualId)
        {
            return;
        }

        var pasoActual = await db.Set<EjecucionPaso>().FirstAsync(p => p.Id == pasoActualId, ct);
        if (pasoActual.Estado is EjecucionPasoEstado.Completado or EjecucionPasoEstado.Fallido or EjecucionPasoEstado.EsperandoRevisionHumana)
        {
            // The step had already reached an outcome before the pause was requested — re-enter the
            // continuation logic instead of re-dispatching a step that already ran.
            await AvanzarAsync(pasoActual.Id, ct);
        }
    }

    public async Task CancelarAsync(Guid casoId, CancellationToken ct)
    {
        var caso = await db.Set<Caso>().FirstOrDefaultAsync(c => c.Id == casoId, ct)
            ?? throw new InvalidOperationException($"Caso {casoId} no encontrado.");

        caso.Estado = CasoEstado.Cancelado;
        caso.UpdatedAt = DateTimeOffset.UtcNow;
        caso.CompletedAt = DateTimeOffset.UtcNow;

        if (caso.EjecucionActualId is not null)
        {
            var ejecucion = await db.Set<Ejecucion>().FirstAsync(e => e.Id == caso.EjecucionActualId, ct);
            ejecucion.Estado = EjecucionEstado.Cancelada;
            ejecucion.FinishedAt = DateTimeOffset.UtcNow;
        }

        await RegistrarEventoAsync(casoId, caso.EjecucionActualId, CasoEventoAccion.Cancelado, null, ct);
    }

    public async Task ResolverRevisionAsync(Guid ejecucionPasoId, RevisionDecision decision, Guid revisorUserId, string? comentario, CancellationToken ct)
    {
        var paso = await db.Set<EjecucionPaso>().FirstOrDefaultAsync(p => p.Id == ejecucionPasoId, ct)
            ?? throw new InvalidOperationException($"EjecucionPaso {ejecucionPasoId} no encontrado.");

        if (paso.Estado != EjecucionPasoEstado.EsperandoRevisionHumana)
        {
            throw new InvalidOperationException("Este paso no está esperando revisión humana.");
        }

        db.Add(new RevisionHumana
        {
            EjecucionPasoId = paso.Id,
            RevisorUserId = revisorUserId,
            Decision = decision,
            Comentario = comentario,
            ResolvedAt = DateTimeOffset.UtcNow,
        });

        paso.Estado = decision == RevisionDecision.Aprobada ? EjecucionPasoEstado.Completado : EjecucionPasoEstado.Fallido;
        paso.FinishedAt = DateTimeOffset.UtcNow;
        if (decision == RevisionDecision.Rechazada)
        {
            paso.ErrorMensaje = comentario ?? "Revisión rechazada.";
        }

        var ejecucion = await db.Set<Ejecucion>().FirstAsync(e => e.Id == paso.EjecucionId, ct);
        var caso = await db.Set<Caso>().FirstAsync(c => c.Id == ejecucion.CasoId, ct);
        await RegistrarEventoAsync(caso.Id, ejecucion.Id, CasoEventoAccion.RevisionResuelta, paso.Id, ct);
        await db.SaveChangesAsync(ct);

        await AvanzarAsync(paso.Id, ct);
    }

    private async Task CrearYDespacharPasoAsync(Ejecucion ejecucion, Caso caso, FlujoPasoDef pasoDef, int numeroIntento, CancellationToken ct)
    {
        var paso = new EjecucionPaso
        {
            EjecucionId = ejecucion.Id,
            CasoId = caso.Id,
            FlujoPasoDefId = pasoDef.Id,
            TipoPaso = pasoDef.TipoPaso,
            NumeroIntento = numeroIntento,
            Estado = EjecucionPasoEstado.Pendiente,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Add(paso);
        ejecucion.PasoActualId = paso.Id;
        await db.SaveChangesAsync(ct);

        await DespacharAsync(paso, pasoDef, caso, ct);
    }

    private async Task DespacharAsync(EjecucionPaso paso, FlujoPasoDef pasoDef, Caso caso, CancellationToken ct)
    {
        paso.Estado = EjecucionPasoEstado.EnProgreso;
        paso.StartedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await RegistrarEventoAsync(caso.Id, paso.EjecucionId, CasoEventoAccion.PasoIniciado, paso.Id, ct);

        var executor = services.GetRequiredKeyedService<IPasoEjecutor>(pasoDef.TipoPaso);
        var context = new PasoEjecucionContext(paso.Id, paso.EjecucionId, caso.Id, pasoDef, caso.DatosJson);

        PasoResultado resultado;
        try
        {
            resultado = await executor.EjecutarAsync(context, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            paso.Estado = EjecucionPasoEstado.Fallido;
            paso.ErrorMensaje = ex.Message;
            paso.FinishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            await AvanzarAsync(paso.Id, ct);
            return;
        }

        if (resultado == PasoResultado.EnProgreso)
        {
            // The executor dispatched real async work (e.g. via IBackgroundTaskQueue) and will
            // finalize this paso's Estado itself before calling AvanzarAsync — nothing more here.
            return;
        }

        paso.Estado = resultado switch
        {
            PasoResultado.Completado => EjecucionPasoEstado.Completado,
            PasoResultado.Fallido => EjecucionPasoEstado.Fallido,
            PasoResultado.EsperandoRevisionHumana => EjecucionPasoEstado.EsperandoRevisionHumana,
            _ => throw new InvalidOperationException($"PasoResultado no soportado: {resultado}"),
        };
        if (resultado != PasoResultado.EsperandoRevisionHumana)
        {
            paso.FinishedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await AvanzarAsync(paso.Id, ct);
    }

    private async Task ContinuarTrasCompletarAsync(Ejecucion ejecucion, Caso caso, EjecucionPaso pasoCompletado, CancellationToken ct)
    {
        var pasoDefCompletado = await db.Set<FlujoPasoDef>().AsNoTracking().FirstAsync(p => p.Id == pasoCompletado.FlujoPasoDefId, ct);

        int siguienteOrden;
        if (pasoDefCompletado.TipoPaso == TipoPaso.Decision)
        {
            var (ordenElegido, ordenOmitido) = ResolverRamaDecision(pasoDefCompletado, caso.DatosJson);
            siguienteOrden = ordenElegido;
            await MarcarRamaOmitidaAsync(ejecucion, caso, pasoDefCompletado.FlujoVersionId, ordenOmitido, ct);
        }
        else
        {
            siguienteOrden = pasoDefCompletado.Orden + 1;
        }

        var siguientePasoDef = await EncontrarSiguientePasoNoOmitidoAsync(ejecucion.Id, pasoDefCompletado.FlujoVersionId, siguienteOrden, ct);

        if (siguientePasoDef is null)
        {
            await FinalizarAsync(ejecucion, caso, EjecucionEstado.Completada, CasoEstado.Completado, CasoEventoAccion.Completado, ct);
            return;
        }

        await CrearYDespacharPasoAsync(ejecucion, caso, siguientePasoDef, numeroIntento: 1, ct);
    }

    /// <summary>Walks forward from <paramref name="desdeOrden"/>, skipping any step already marked
    /// Omitido for this Ejecucion (the untaken side of an earlier Decision) until it finds one to
    /// actually run, or runs out of steps. This is what lets a flow "converge" after a branch (e.g.
    /// OK/ERROR both leading to a shared Finalización step) under the V1 flat-Orden model — without
    /// it, completing the taken branch would just fall through into the untaken branch's own steps.</summary>
    private async Task<FlujoPasoDef?> EncontrarSiguientePasoNoOmitidoAsync(Guid ejecucionId, Guid flujoVersionId, int desdeOrden, CancellationToken ct)
    {
        var orden = desdeOrden;
        while (true)
        {
            var candidato = await db.Set<FlujoPasoDef>().AsNoTracking()
                .FirstOrDefaultAsync(p => p.FlujoVersionId == flujoVersionId && p.Orden == orden, ct);
            if (candidato is null)
            {
                return null;
            }

            var yaOmitido = await db.Set<EjecucionPaso>().AnyAsync(p =>
                p.EjecucionId == ejecucionId && p.FlujoPasoDefId == candidato.Id && p.Estado == EjecucionPasoEstado.Omitido, ct);
            if (!yaOmitido)
            {
                return candidato;
            }

            orden++;
        }
    }

    /// <summary>Reads ordenSiVerdadero/ordenSiFalso from the Decision step's ConfiguracionJson and
    /// picks one by looking up "condicion" as a flat boolean property on the Caso's DatosJson — a
    /// deliberately simple V1 rule language, not a general expression evaluator. Returns (elegido,
    /// omitido) so the caller can record a trace row for the branch that was NOT taken.</summary>
    private static (int Elegido, int Omitido) ResolverRamaDecision(FlujoPasoDef decisionDef, string? datosJsonCaso)
    {
        using var configuracion = JsonDocument.Parse(decisionDef.ConfiguracionJson ?? "{}");
        var root = configuracion.RootElement;
        var condicion = root.TryGetProperty("condicion", out var condicionProp) ? condicionProp.GetString() : null;
        var ordenSiVerdadero = root.GetProperty("ordenSiVerdadero").GetInt32();
        var ordenSiFalso = root.GetProperty("ordenSiFalso").GetInt32();

        var esVerdadero = false;
        if (condicion is not null && !string.IsNullOrWhiteSpace(datosJsonCaso))
        {
            using var datos = JsonDocument.Parse(datosJsonCaso);
            if (datos.RootElement.TryGetProperty(condicion, out var valor) && valor.ValueKind == JsonValueKind.True)
            {
                esVerdadero = true;
            }
        }

        return esVerdadero ? (ordenSiVerdadero, ordenSiFalso) : (ordenSiFalso, ordenSiVerdadero);
    }

    /// <summary>
    /// Independent scripts (Rpa/Api/Agente/Interno steps) opt in — per step, if they want to — to
    /// reporting a business status by putting "estadoNegocioCodigo" in their FlujoPasoDef's
    /// ConfiguracionJson. A step that never mentions it leaves Caso.EstadoNegocioActualId untouched.
    /// This is purely informational: it never influences what the engine does next.
    /// </summary>
    private async Task AplicarEstadoNegocioSiCorrespondeAsync(Caso caso, EjecucionPaso paso, CancellationToken ct)
    {
        var pasoDef = await db.Set<FlujoPasoDef>().AsNoTracking().FirstOrDefaultAsync(p => p.Id == paso.FlujoPasoDefId, ct);
        if (string.IsNullOrWhiteSpace(pasoDef?.ConfiguracionJson))
        {
            return;
        }

        string? codigo;
        try
        {
            using var configuracion = JsonDocument.Parse(pasoDef.ConfiguracionJson);
            codigo = configuracion.RootElement.TryGetProperty("estadoNegocioCodigo", out var prop) ? prop.GetString() : null;
        }
        catch (JsonException)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return;
        }

        var estado = await db.Set<FlujoEstadoDef>()
            .FirstOrDefaultAsync(e => e.FlujoId == caso.FlujoId && e.Codigo == codigo && e.Activo, ct);
        if (estado is null || estado.Id == caso.EstadoNegocioActualId)
        {
            return;
        }

        caso.EstadoNegocioActualId = estado.Id;
        caso.UpdatedAt = DateTimeOffset.UtcNow;
        db.Add(new CasoEvento
        {
            CasoId = caso.Id,
            EjecucionId = paso.EjecucionId,
            Accion = CasoEventoAccion.EstadoNegocioActualizado,
            DetalleJson = JsonSerializer.Serialize(new { estado.Codigo, estado.Display }),
            OccurredAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Records the Decision branch that was NOT taken as a terminal Omitido row — purely for
    /// traceability (e.g. a future flow diagram showing the skipped path grayed out); the orchestrator
    /// never dispatches it.</summary>
    private async Task MarcarRamaOmitidaAsync(Ejecucion ejecucion, Caso caso, Guid flujoVersionId, int ordenOmitido, CancellationToken ct)
    {
        var pasoDefOmitido = await db.Set<FlujoPasoDef>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.FlujoVersionId == flujoVersionId && p.Orden == ordenOmitido, ct);
        if (pasoDefOmitido is null)
        {
            return;
        }

        db.Add(new EjecucionPaso
        {
            EjecucionId = ejecucion.Id,
            CasoId = caso.Id,
            FlujoPasoDefId = pasoDefOmitido.Id,
            TipoPaso = pasoDefOmitido.TipoPaso,
            NumeroIntento = 1,
            Estado = EjecucionPasoEstado.Omitido,
            CreatedAt = DateTimeOffset.UtcNow,
            FinishedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task FinalizarAsync(
        Ejecucion ejecucion, Caso caso, EjecucionEstado ejecucionEstado, CasoEstado casoEstado, CasoEventoAccion evento, CancellationToken ct)
    {
        ejecucion.Estado = ejecucionEstado;
        ejecucion.FinishedAt = DateTimeOffset.UtcNow;
        caso.Estado = casoEstado;
        caso.UpdatedAt = DateTimeOffset.UtcNow;
        caso.CompletedAt = DateTimeOffset.UtcNow;

        await RegistrarEventoAsync(caso.Id, ejecucion.Id, evento, null, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task RegistrarEventoAsync(Guid casoId, Guid? ejecucionId, CasoEventoAccion accion, Guid? pasoId, CancellationToken ct)
    {
        db.Add(new CasoEvento
        {
            CasoId = casoId,
            EjecucionId = ejecucionId,
            Accion = accion,
            DetalleJson = pasoId is null ? null : JsonSerializer.Serialize(new { EjecucionPasoId = pasoId }),
            OccurredAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}
