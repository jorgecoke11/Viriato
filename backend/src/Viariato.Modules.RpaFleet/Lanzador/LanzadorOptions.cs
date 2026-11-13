namespace Viariato.Modules.RpaFleet.Lanzador;

/// <summary>
/// The launcher that runs next to Viriato (section <c>Lanzador</c>). Everything has a default: in production the compose sets the
/// secret's file and the public address, and nothing else needs touching.
/// </summary>
public sealed class LanzadorOptions
{
    public const string SectionName = "Lanzador";

    /// <summary>
    /// File with the secret shared with the launcher. Created (32 random bytes) the first time if it is not there; both containers
    /// mount the same volume. Empty: there is no launcher next to Viriato, and its catalogue answers 404.
    /// </summary>
    public string ArchivoSecreto { get; set; } = string.Empty;

    /// <summary>Where uploaded robots are kept. Empty: a <c>robots</c> folder in the documents' storage (so the backup takes them).</summary>
    public string CarpetaDeRobots { get; set; } = string.Empty;

    /// <summary>Copies of any robot the launcher may run at once.</summary>
    public int MaxRobotsALaVez { get; set; } = 2;

    /// <summary>Of those, how many with a browser (they are the heavy ones).</summary>
    public int MaxConPantalla { get; set; } = 1;

    /// <summary>The address people open Viriato at (PUBLIC_URL): a robot with a browser is told where its live view is seen from.</summary>
    public string? UrlPublica { get; set; }

    /// <summary>How long a pass handed to a robot is valid. It is renewed every round; a copy keeps the one it started with.</summary>
    public int DiasDelPase { get; set; } = 7;

    /// <summary>The biggest robot zip accepted, in MB.</summary>
    public int TamanoMaximoMb { get; set; } = 120;
}
