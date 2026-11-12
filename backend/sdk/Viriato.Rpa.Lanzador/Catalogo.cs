namespace Viriato.Rpa.Lanzador;

/// <summary>
/// The launcher's whole configuration: the catalogue file (<c>robots.json</c>). It says where Viriato is, how much may run at
/// once on this machine, and which robots there are. Secrets are never in it: each robot names the environment variable that
/// holds its key.
/// </summary>
public sealed class LanzadorOptions
{
    /// <summary>Address of the Viriato API as seen from the launcher (the variable <c>VIRIATO_URL</c> wins over the file).</summary>
    public string ViriatoUrl { get; set; } = string.Empty;

    /// <summary>How often it asks Viriato whether each robot has work, and looks after what is running.</summary>
    public int IntervaloSegundos { get; set; } = 5;

    /// <summary>The machine's ceiling: copies of any robot running at the same time.</summary>
    public int MaxRobotsALaVez { get; set; } = 2;

    /// <summary>Of those, how many may have a screen (a browser): they are the heavy ones.</summary>
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

    /// <summary>Everything wrong with the catalogue, in words whoever edits it can act on. Empty means it is usable.</summary>
    /// <param name="variable">Reads an environment variable (injected so it can be checked without touching the real ones).</param>
    public IReadOnlyList<string> Validar(Func<string, string?> variable)
    {
        var problemas = new List<string>();

        if (string.IsNullOrWhiteSpace(ViriatoUrl))
        {
            problemas.Add("Falta la dirección de Viriato: \"viriatoUrl\" en el catálogo o la variable VIRIATO_URL.");
        }
        else if (!Uri.TryCreate(ViriatoUrl, UriKind.Absolute, out var url) || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            problemas.Add($"La dirección de Viriato no es una URL http(s) válida: '{ViriatoUrl}'.");
        }

        if (IntervaloSegundos < 1) problemas.Add("\"intervaloSegundos\" debe ser 1 o más.");
        if (MaxRobotsALaVez < 1) problemas.Add("\"maxRobotsALaVez\" debe ser 1 o más.");
        if (MaxConPantalla < 0) problemas.Add("\"maxConPantalla\" no puede ser negativo.");
        if (SalirSinTrabajoSegundos < 1) problemas.Add("\"salirSinTrabajoSegundos\" debe ser 1 o más.");
        if (Robots.Count == 0) problemas.Add("El catálogo no tiene ningún robot (\"robots\").");

        var nombres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < Robots.Count; i++)
        {
            var robot = Robots[i];
            var quien = string.IsNullOrWhiteSpace(robot.Nombre) ? $"El robot nº {i + 1}" : $"El robot \"{robot.Nombre}\"";

            if (string.IsNullOrWhiteSpace(robot.Nombre)) problemas.Add($"{quien} no tiene \"nombre\".");
            else if (!nombres.Add(robot.Nombre)) problemas.Add($"Hay dos robots llamados \"{robot.Nombre}\".");

            if (string.IsNullOrWhiteSpace(robot.Comando)) problemas.Add($"{quien} no tiene \"comando\" (p. ej. \"dotnet\").");
            if (robot.MaxCopias < 0) problemas.Add($"{quien}: \"maxCopias\" no puede ser negativo.");
            if (robot.MemoriaMaximaMb is <= 0) problemas.Add($"{quien}: \"memoriaMaximaMb\" debe ser mayor que 0 (o no ponerse).");

            if (string.IsNullOrWhiteSpace(robot.ClaveEnv))
            {
                problemas.Add($"{quien} no dice en qué variable de entorno está su clave (\"claveEnv\").");
            }
            else if (string.IsNullOrWhiteSpace(variable(robot.ClaveEnv)))
            {
                problemas.Add($"{quien}: la variable de entorno {robot.ClaveEnv} está vacía; ahí va la clave de su despliegue en Viriato.");
            }
        }

        return problemas;
    }
}

/// <summary>One robot the launcher can start: how, how many, and where its key is.</summary>
public sealed class RobotOptions
{
    /// <summary>Name for the logs and the temporary folder. Unique in the catalogue.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Name of the environment variable (of the launcher) that holds the key of this robot's Despliegue.</summary>
    public string ClaveEnv { get; set; } = string.Empty;

    /// <summary>The program to run, e.g. <c>dotnet</c>.</summary>
    public string Comando { get; set; } = string.Empty;

    /// <summary>Its arguments, e.g. <c>["/robots/bsh-extractor/Bsh.PricesExtractor.dll"]</c>.</summary>
    public List<string> Argumentos { get; set; } = [];

    /// <summary>Folder it is started in. Empty: the launcher's own.</summary>
    public string? Directorio { get; set; }

    /// <summary>Copies of this robot that may run at once here. 0 = never started (a way to park it).</summary>
    public int MaxCopias { get; set; } = 1;

    /// <summary>It needs a screen (it drives a browser): it gets a virtual one of its own (Xvfb + a window manager).</summary>
    public bool Pantalla { get; set; }

    /// <summary>A copy using more memory than this — it and everything it started — is killed. Empty: no limit.</summary>
    public int? MemoriaMaximaMb { get; set; }

    /// <summary>
    /// Extra environment variables for the robot (its own settings, <c>Seccion__Clave</c>). A value may use <c>${VARIABLE}</c>
    /// (a variable of the launcher) and, for a robot with a screen, <c>{puertoWeb}</c>, <c>{puertoVnc}</c> and <c>{pantalla}</c>.
    /// </summary>
    public Dictionary<string, string> Entorno { get; set; } = [];
}
