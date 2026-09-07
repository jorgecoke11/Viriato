using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.Orchestration;

/// <summary>
/// The brain: decides what runs next for a Caso, when, and what to do on failure — workers never
/// hold this logic themselves, they only execute one step and report back.
/// </summary>
public interface IEjecucionOrchestrator
{
    /// <summary>Creates the first Ejecucion for a Caso and dispatches its first step — or, if
    /// <paramref name="pasoInicialId"/> is given, dispatches that step directly and records every
    /// FlujoPasoDef before it as Omitido (e.g. "the documentation was already validated externally").</summary>
    Task<Guid> IniciarCasoAsync(Guid casoId, Guid? pasoInicialId, CancellationToken ct);

    /// <summary>Reads the (already finalized) Estado of one EjecucionPaso and decides what happens
    /// next — advance to the following step, finish the Ejecucion, or park it waiting.</summary>
    Task AvanzarAsync(Guid ejecucionPasoId, CancellationToken ct);

    /// <summary>Reprocesses the latest attempt of a step — whether it ended Completado, Fallido or
    /// Cancelado — by creating a new attempt row and redispatching it; the old one is never mutated.
    /// This also re-opens the owning Ejecucion/Caso back to EnProgreso, even if the Caso had already
    /// finished.</summary>
    Task ReprocesarPasoAsync(Guid ejecucionPasoId, CancellationToken ct);

    /// <summary>Closes the Caso right now as Completado, called explicitly by whichever step's worker
    /// knows it is the true end of the cycle — deliberately skips the normal Orden-position walk
    /// (<see cref="AvanzarAsync"/> would instead look for the next FlujoPasoDef), so it also finishes a
    /// Caso whose Flujo still has steps defined after this one.</summary>
    Task CompletarCasoAsync(Guid ejecucionPasoId, CancellationToken ct);

    Task PausarAsync(Guid casoId, CancellationToken ct);

    /// <summary>Resumes a paused Ejecucion, re-entering the exact attempt that was in flight.</summary>
    Task ReanudarAsync(Guid casoId, CancellationToken ct);

    Task CancelarAsync(Guid casoId, CancellationToken ct);

    /// <summary>Records a human reviewer's decision on a step waiting in RevisionHumana and continues
    /// (Aprobada) or fails (Rechazada) the Ejecucion accordingly.</summary>
    Task ResolverRevisionAsync(Guid ejecucionPasoId, RevisionDecision decision, Guid revisorUserId, string? comentario, CancellationToken ct);
}
