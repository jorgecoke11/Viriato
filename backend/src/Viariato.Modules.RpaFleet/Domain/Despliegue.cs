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

    public required string ApiKeyHash { get; set; }

    public required string ApiKeyPrefix { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }
}
