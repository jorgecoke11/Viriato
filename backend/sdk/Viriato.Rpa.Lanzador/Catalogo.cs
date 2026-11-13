namespace Viriato.Rpa.Lanzador;

/// <summary>
/// What the launcher works with in a round: how it behaves (fixed, its own defaults) and which robots it runs, as Viriato says
/// (<see cref="CatalogoDeViriato"/>): the machine's ceilings and, for each robot, how to run it and its replicas.
/// </summary>
public sealed class LanzadorOptions
{
    /// <summary>Address of the Viriato API as seen from the launcher (variable <c>VIRIATO_URL</c>).</summary>
    public string ViriatoUrl { get; set; } = string.Empty;

    /// <summary>How often it asks Viriato whether each robot has work, and looks after what is running.</summary>
    public int IntervaloSegundos { get; set; } = 5;

    /// <summary>The machine's ceiling: copies of any robot running at the same time (set in Viriato).</summary>
    public int MaxRobotsALaVez { get; set; } = 2;

    /// <summary>Of those, how many may have a screen (a browser): they are the heavy ones (set in Viriato).</summary>
    public int MaxConPantalla { get; set; } = 1;

    /// <summary>What a started robot is told: leave after this long without being handed a step (<c>Worker:SalirSinTrabajo</c>).</summary>
    public int SalirSinTrabajoSegundos { get; set; } = 30;

    /// <summary>After a robot fails to start or dies with an error, how long before trying again; doubles with each failure
    /// in a row, up to fifteen minutes.</summary>
    public int EsperaTrasFalloSegundos { get; set; } = 60;

    /// <summary>
    /// A copy that is running while Viriato says its robot neither holds a step nor has any waiting should have left by itself
    /// (<see cref="SalirSinTrabajoSegundos"/>). If it is still there this much later it is hung — or built with a client older
    /// than 0.12, which never leaves — and it is stopped.
    /// </summary>
    public int GraciaParaSalirSegundos { get; set; } = 60;

    /// <summary>
    /// A robot with work waiting and an idle copy that does not take it for this long is restarted: the copy is hung. Not when
    /// Viriato is holding the work back on purpose (service at its cap, Despliegue off). 0 = never.
    /// </summary>
    public int ReiniciarSiNoCogeTrabajoSegundos { get; set; } = 300;

    /// <summary>How long a robot gets to finish what it is doing when told to stop, before it is killed.</summary>
    public int GraciaAlPararSegundos { get; set; } = 30;

    /// <summary>Each copy gets its own temporary folder under this one, deleted when the copy ends.</summary>
    public string CarpetaTemporal { get; set; } = "/tmp/lanzador";

    /// <summary>First X display handed to a robot with a screen; the second such copy gets the next one.</summary>
    public int PantallaBase { get; set; } = 99;

    /// <summary>Port of the live view (noVNC) of the first robot with a screen; the second gets the next one.</summary>
    public int PuertoWebBase { get; set; } = 6080;

    public int PuertoVncBase { get; set; } = 5900;

    public List<RobotOptions> Robots { get; set; } = [];
}

/// <summary>One robot the launcher can start: how, and how many at once.</summary>
public sealed class RobotOptions
{
    /// <summary>Name for the logs and the temporary folder. Unique.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>The program to run, e.g. <c>dotnet</c>.</summary>
    public string Comando { get; set; } = string.Empty;

    /// <summary>Its arguments, e.g. <c>["Bsh.PricesExtractor.dll"]</c>.</summary>
    public List<string> Argumentos { get; set; } = [];

    /// <summary>Folder it is started in: where its version was unpacked.</summary>
    public string? Directorio { get; set; }

    /// <summary>Replicas: copies of this robot that may run at once (set on its Despliegue). 0 = parked.</summary>
    public int Replicas { get; set; } = 1;

    /// <summary>It needs a screen (it drives a browser): it gets a virtual one of its own (Xvfb + a window manager).</summary>
    public bool Pantalla { get; set; }

    /// <summary>A copy using more memory than this — it and everything it started — is killed. Empty: no limit.</summary>
    public int? MemoriaMaximaMb { get; set; }

    /// <summary>Extra environment variables for the robot (its own settings, <c>Seccion__Clave</c>). For a robot with a screen a
    /// value may use <c>{puertoWeb}</c>, <c>{puertoVnc}</c> and <c>{pantalla}</c>.</summary>
    public Dictionary<string, string> Entorno { get; set; } = [];
}
