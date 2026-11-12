namespace Viariato.ApiContracts;

/// <summary>What GET /api/v1/rpa/lanzador/trabajo answers: whether it is worth having a copy of this robot running right now.
/// Asked by the launcher (the process that starts robots on demand) with the robot's own key, not by the robot.</summary>
/// <param name="Encendido">The Despliegue's switch: off means no copy should be started.</param>
/// <param name="Pendientes">Steps waiting in this robot's queue that nobody has taken.</param>
/// <param name="EnEjecucion">Steps a copy of this robot holds right now (still within their service's maximum time).</param>
/// <param name="ServicioAlLimite">The service is at its global cap: a copy started now would be handed nothing.</param>
public sealed record TrabajoDelDespliegueDto(bool Encendido, int Pendientes, int EnEjecucion, bool ServicioAlLimite);

/// <summary>What the launcher tells Viriato about one robot (PUT /api/v1/rpa/lanzador/estado), so the machine's screen can
/// say what is started and, when something is not, why.</summary>
/// <param name="Estado">One of <see cref="EstadosDelLanzador"/>.</param>
/// <param name="Copias">Copies of the robot running on the launcher right now.</param>
/// <param name="MaxCopias">The most the launcher will run of it at once.</param>
/// <param name="Detalle">The reason, in words for a person (why it waits, what failed). Optional.</param>
public sealed record EstadoDelLanzadorRequest(string Estado, int Copias, int MaxCopias, string? Detalle = null);

/// <summary>The situations a launcher reports a robot in.</summary>
public static class EstadosDelLanzador
{
    /// <summary>Nothing waiting: no copy is running, and that is fine.</summary>
    public const string SinTrabajo = "SinTrabajo";

    /// <summary>At least one copy is running.</summary>
    public const string EnMarcha = "EnMarcha";

    /// <summary>There is work but no copy can be started yet (the launcher is at its ceiling, the service at its cap…).</summary>
    public const string EnEspera = "EnEspera";

    /// <summary>The Despliegue is switched off in Viriato.</summary>
    public const string Apagado = "Apagado";

    /// <summary>The robot could not be started, or keeps dying as soon as it starts.</summary>
    public const string Fallo = "Fallo";

    public static readonly IReadOnlyList<string> Todos = [SinTrabajo, EnMarcha, EnEspera, Apagado, Fallo];
}
