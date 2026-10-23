using System.Text.Json;
using Viariato.ApiContracts;

namespace Viriato.Rpa.Client;

/// <summary>
/// Everything a robot needs to talk to Viriato: check whether its Despliegue is switched on, pull the
/// next pending execution off its queue, and report back — complete, fail, attach evidence, or change
/// the owning Caso's business state. Every write is scoped to the <c>EjecucionPasoId</c> handed back by
/// <see cref="ObtenerSiguienteEjecucionAsync"/>; the server rejects anything the caller didn't
/// legitimately claim. An API error status surfaces as <see cref="ViriatoApiException"/>.
/// </summary>
public interface IRpaClient
{
    /// <summary>Whether this Despliegue is switched on, plus the Equipo/Servicio/Flujo it belongs to.</summary>
    Task<DespliegueEstadoDto> ObtenerEstadoAsync(CancellationToken ct = default);

    /// <summary>Shortcut for <c>(await ObtenerEstadoAsync()).Encendido</c>.</summary>
    Task<bool> IsActivoAsync(CancellationToken ct = default);

    /// <summary>
    /// Claims the oldest pending step for this Despliegue. Null means the queue is empty right now —
    /// not an error, just nothing to do yet. Once this returns a step it is yours: nobody else will be
    /// handed it, so always finish it with <see cref="CompletarPasoAsync"/>, <see cref="CompletarCasoAsync"/>
    /// or <see cref="FallarPasoAsync"/>. Throws <see cref="ViriatoApiException"/> (conflict) if the
    /// Despliegue is switched off.
    /// </summary>
    Task<EjecucionAsignadaDto?> ObtenerSiguienteEjecucionAsync(CancellationToken ct = default);

    /// <summary>Marks the step done; the Caso moves on to its next step (or finishes if there is none).</summary>
    Task CompletarPasoAsync(Guid ejecucionPasoId, string? parametrosSalida = null, CancellationToken ct = default);

    /// <summary>
    /// For the one step that knows it is the true end of the cycle: completes this step AND closes the
    /// whole Caso as Completado right now, regardless of whether the Flujo defines more steps after it.
    /// Use <see cref="CompletarPasoAsync"/> for a step that is just one link in the chain.
    /// </summary>
    Task CompletarCasoAsync(Guid ejecucionPasoId, string? parametrosSalida = null, CancellationToken ct = default);

    /// <summary>Marks the step as failed with a human-readable reason.</summary>
    Task FallarPasoAsync(Guid ejecucionPasoId, string error, CancellationToken ct = default);

    /// <summary>Sets the Caso's business state to the Flujo state with this <paramref name="codigoEstado"/>.</summary>
    Task CambiarEstadoCasoAsync(Guid ejecucionPasoId, string codigoEstado, CancellationToken ct = default);

    /// <summary>
    /// Writes what this step found into the business data of the Caso, so the next step can work from it (a robot reads the data in
    /// <c>DatosCasoJson</c> of what it claims). <paramref name="datosJson"/> is a JSON object: each of its properties replaces the one of the
    /// same name in the Caso's data and the rest stay. This is how one service hands the baton to the next within a Caso.
    /// </summary>
    Task ActualizarDatosCasoAsync(Guid ejecucionPasoId, string datosJson, CancellationToken ct = default);

    /// <summary>
    /// Tells Viriato how the step is going so that people can follow it: how far along it is (<paramref name="porcentaje"/>, 0 to
    /// 100), what it is doing now (<paramref name="mensaje"/>), and where the robot's screen can be watched live
    /// (<paramref name="vistaUrl"/>, an http(s) address). Only what is sent changes; an empty text clears it. Viriato keeps the latest
    /// figure and writes the milestones (a new phase, every tenth of the way) in the Caso's history.
    /// </summary>
    Task ReportarEnVivoAsync(Guid ejecucionPasoId, int? porcentaje = null, string? mensaje = null, string? vistaUrl = null, CancellationToken ct = default);

    /// <summary>
    /// The settings of the process this robot's Despliegue belongs to (Viriato's "Parámetros" tab). Always the
    /// robot's own process — it is decided by the API key, not by anything the robot sends. Not secret: use
    /// <see cref="ObtenerCredencialAsync"/> for passwords.
    /// </summary>
    Task<ParametrosProceso> ObtenerParametrosAsync(CancellationToken ct = default);

    /// <summary>
    /// Asks Viriato for the credential stored under <paramref name="nombre"/> (case-insensitive). Only the
    /// credentials this robot's Servicio may use are served: one that is missing, inactive or someone else's
    /// is the same <see cref="ViriatoApiException"/> with <c>IsNotFound</c>, and a switched-off Despliegue
    /// gets a conflict. The password is never logged by this library; do not log it either.
    /// </summary>
    Task<CredencialRobotDto> ObtenerCredencialAsync(string nombre, CancellationToken ct = default);

    /// <summary>
    /// Opens a new Caso in the process this Despliegue was authorised to create Cases in (set by an admin in
    /// Viriato's Despliegues screen); the request cannot name any other. A Despliegue without that permission
    /// gets a <see cref="ViriatoApiException"/> with <c>IsForbidden</c>, and one whose target process has no
    /// published version, case type, business state or creator of that name, a conflict — as does data that does not fit
    /// the form of the creator it names (<see cref="CrearCasoRobotRequest.Creador"/>). Calling this twice creates two
    /// Cases: it is not idempotent, so a launcher that can be retried should keep track of what it already made.
    /// </summary>
    Task<CasoCreadoDto> CrearCasoAsync(CrearCasoRobotRequest request, CancellationToken ct = default);

    /// <summary>
    /// Attaches evidence (a screenshot, video, generated file or extracted data) to the step.
    /// <paramref name="archivo"/> is read to the end and then closed by this call.
    /// </summary>
    Task AgregarEvidenciaAsync(
        Guid ejecucionPasoId,
        EvidenciaTipo tipo,
        string titulo,
        string? contenidoJson = null,
        Stream? archivo = null,
        string? nombreArchivo = null,
        CancellationToken ct = default);

    /// <summary>
    /// Says something happened that may deserve a notice — e.g. <c>precio_bajado</c> with <c>{ producto, antes, ahora }</c>. It goes into
    /// the case's history, and the process's notification rules that listen for <paramref name="evento"/> send their notices (by mail…),
    /// written with <c>{evento.campo}</c> from <paramref name="datos"/>. With no rule listening nothing is sent, and it is not an error.
    /// (A default body so test doubles written before 0.9 keep compiling.)
    /// </summary>
    Task NotificarAsync(Guid ejecucionPasoId, string evento, object? datos = null, CancellationToken ct = default) =>
        throw new NotSupportedException("Este cliente no sabe notificar (Viriato.Rpa.Client 0.9+).");

    /// <summary>What the process's robots kept under <paramref name="clave"/> (letters, digits, <c>. _ : -</c>), or null if nothing.
    /// Memory between cases, shared by every robot of the process: the last price seen, the last page read…</summary>
    Task<JsonElement?> ObtenerDatoAsync(string clave, CancellationToken ct = default) =>
        throw new NotSupportedException("Este cliente no tiene almacén (Viriato.Rpa.Client 0.9+).");

    /// <summary>Keeps <paramref name="valor"/> (any JSON, up to 64 KB) under <paramref name="clave"/> for the process's robots, replacing
    /// what was there.</summary>
    Task GuardarDatoAsync(string clave, object valor, CancellationToken ct = default) =>
        throw new NotSupportedException("Este cliente no tiene almacén (Viriato.Rpa.Client 0.9+).");
}
