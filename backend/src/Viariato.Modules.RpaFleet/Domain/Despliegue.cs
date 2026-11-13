namespace Viariato.Modules.RpaFleet.Domain;

/// <summary>One Servicio deployed on one Equipo for one Flujo (proceso) — the unit the worker API
/// authenticates as. The same Servicio can have several Despliegues on the same Equipo, one per
/// Flujo it serves (e.g. one robot installed once, handling several distinct procesos), so the
/// uniqueness boundary is the (Equipo, Servicio, Flujo) triple, not just (Equipo, Servicio).
///
/// FlujoId is a bare, unconstrained Guid — Flujo lives in Viariato.Modules.Flujos, which itself
/// references THIS module (for FlujoPasoDef.ServicioId), so RpaFleet can't reference Flujos back
/// without a cycle. Same convention as AsignacionFlujo.UserId elsewhere in the codebase: whoever
/// needs the referenced entity's details resolves them from their own module's AppDbContext access.
///
/// Its API key is stored only as a SHA-256 hash (ApiKeyHash); ApiKeyPrefix is the raw key's first
/// few characters, kept only so an admin can tell deployments apart in a list without re-exposing
/// the secret.</summary>
public sealed class Despliegue
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EquipoId { get; set; }

    public Equipo? Equipo { get; set; }

    public Guid ServicioId { get; set; }

    public Servicio? Servicio { get; set; }

    public Guid FlujoId { get; set; }

    /// <summary>The one process this Despliegue's robot may create Casos in (a "launcher" robot such as the
    /// one that fans a request out into one Caso per product). Null — the default — means it may not create
    /// any: the worker API otherwise only ever touches the Casos it was handed. Bare Guid for the same
    /// module-cycle reason as <see cref="FlujoId"/>.</summary>
    public Guid? FlujoDestinoId { get; set; }

    /// <summary>Master on/off switch a worker checks before pulling from the queue.</summary>
    public bool Encendido { get; set; } = true;

    /// <summary>Whether its robot's screen can be watched live, and how smoothly. Low by default: watching costs the robot CPU.</summary>
    public CalidadVistaEnDirecto VistaEnDirecto { get; set; } = CalidadVistaEnDirecto.Baja;

    /// <summary>Whether its robot records each run as a video; <see cref="GrabacionDeVideo.SegunRobot"/> leaves it to the robot.</summary>
    public GrabacionDeVideo GrabacionDeVideo { get; set; } = GrabacionDeVideo.SegunRobot;

    public required string ApiKeyHash { get; set; }

    public required string ApiKeyPrefix { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>What a launcher (the process that starts this robot only while it has work) last said about it: one of
    /// <c>EstadosDelLanzador</c>. Null for a robot nobody launches on demand — one that is simply left running.</summary>
    public string? LanzadorEstado { get; set; }

    /// <summary>Copies of the robot the launcher had running when it last reported.</summary>
    public int? LanzadorCopias { get; set; }

    /// <summary>Why it is in that state, in words (why it waits, what failed).</summary>
    public string? LanzadorDetalle { get; set; }

    /// <summary>When the launcher last reported. Old = the launcher itself is gone, and so is whatever it said.</summary>
    public DateTimeOffset? LanzadorVistoEn { get; set; }

    /// <summary>Whether the launcher next to Viriato runs this Despliegue's robot (with its service's program). False — the
    /// default — means that launcher leaves it alone: the robot runs somewhere else, with its own key.</summary>
    public bool EnElLanzador { get; set; }

    /// <summary>The version of its service's program it is held at. Null — the default — follows the latest one uploaded.</summary>
    public Guid? VersionDeRobotId { get; set; }

    public VersionDeRobot? VersionDeRobot { get; set; }

    /// <summary>The version the launcher runs for it: the one it is held at, or its service's latest; null with none uploaded or
    /// when the launcher does not run it. Needs <see cref="Servicio"/> and its versions loaded.</summary>
    public VersionDeRobot? VersionQueCorre => !EnElLanzador || Servicio is null
        ? null
        : VersionDeRobotId is { } fijada ? Servicio.Versiones.FirstOrDefault(v => v.Id == fijada) : Servicio.UltimaVersion;

    /// <summary>Replicas: copies of its robot the launcher may run at once when it has work. Set here, never by the robot's code;
    /// 0 parks it.</summary>
    public int Replicas { get; set; } = 1;

    /// <summary>A copy of its robot using more memory than this is stopped. Null: what its version says (2048 MB with a browser).</summary>
    public int? MemoriaMaximaMb { get; set; }
}
