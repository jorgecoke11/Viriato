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

/// <summary>
/// What GET /api/v1/rpa/lanzador/catalogo answers to the launcher that lives next to Viriato (it proves it with the shared secret,
/// header <see cref="RpaHeaders.SecretoDelLanzador"/>): which robots it runs, how, and a pass for each. Read every round, so a
/// version uploaded in Viriato, or a robot switched to another version, takes effect without touching the server.
/// </summary>
/// <param name="MaxRobotsALaVez">The machine's ceiling: copies of any robot running at once.</param>
/// <param name="MaxConPantalla">Of those, how many may drive a browser.</param>
public sealed record CatalogoDelLanzadorDto(int MaxRobotsALaVez, int MaxConPantalla, IReadOnlyList<RobotDelCatalogoDto> Robots);

/// <summary>One robot of the catalogue: one Despliegue with a robot version uploaded to Viriato.</summary>
/// <param name="Nombre">For the logs and the screen: the service's name (and the process's, when the service has several).</param>
/// <param name="Clave">A pass that authenticates as this Despliegue for a few days. The launcher asks with it and hands it to the
/// copies it starts, so no key is ever written anywhere.</param>
/// <param name="VersionId">The version to run; its package is at GET /api/v1/rpa/lanzador/versiones/{id}/paquete.</param>
/// <param name="Sha256">Of the package, to check the download.</param>
/// <param name="Raiz">Folder inside the package the robot lives in ("" = its root): a zip of the folder rather than of its contents.</param>
/// <param name="Comando">The program to run, e.g. <c>dotnet</c>.</param>
/// <param name="Argumentos">Its arguments, relative to <paramref name="Raiz"/>, e.g. <c>["Bsh.PricesExtractor.dll"]</c>.</param>
/// <param name="Pantalla">It drives a browser: it gets a virtual screen of its own.</param>
/// <param name="MemoriaMaximaMb">A copy using more than this is stopped. Null: no limit.</param>
/// <param name="MaxCopias">Copies of it that may run at once.</param>
/// <param name="Entorno">Extra settings for the robot (environment variables, <c>Seccion__Clave</c>).</param>
public sealed record RobotDelCatalogoDto(
    Guid DespliegueId,
    string Nombre,
    string Clave,
    Guid VersionId,
    int Numero,
    string Sha256,
    string Raiz,
    string Comando,
    IReadOnlyList<string> Argumentos,
    bool Pantalla,
    int? MemoriaMaximaMb,
    int MaxCopias,
    IReadOnlyDictionary<string, string> Entorno);
