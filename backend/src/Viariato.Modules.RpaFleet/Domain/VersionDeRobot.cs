namespace Viariato.Modules.RpaFleet.Domain;

/// <summary>
/// One upload of a robot's program (the zip of its <c>dotnet publish</c>) to its Servicio: the program is what the service is, so
/// every Despliegue of it shares the same versions. The launcher that runs next to Viriato runs, for each Despliegue it starts, the
/// latest one or the one that Despliegue is held at; the others stay so it can go back with a click. How to run it is worked out
/// from the zip when it is uploaded (<see cref="Lanzador.PaqueteDeRobot"/>), so nobody has to describe it.
/// </summary>
public sealed class VersionDeRobot
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ServicioId { get; set; }

    /// <summary>1, 2, 3… within its Servicio, for people. The highest is the latest.</summary>
    public int Numero { get; set; }

    /// <summary>The name of the file that was uploaded.</summary>
    public required string NombreArchivo { get; set; }

    public long TamanoBytes { get; set; }

    /// <summary>Of the zip, hex. The launcher checks its download against it.</summary>
    public required string Sha256 { get; set; }

    /// <summary>Where the zip is, relative to the robots' folder (<see cref="Lanzador.AlmacenDeRobots"/>).</summary>
    public required string Ruta { get; set; }

    /// <summary>Folder inside the zip the robot lives in; empty when the zip holds the files themselves.</summary>
    public string Raiz { get; set; } = string.Empty;

    public required string Comando { get; set; }

    public List<string> Argumentos { get; set; } = [];

    public bool Pantalla { get; set; }

    public int? MemoriaMaximaMb { get; set; }

    /// <summary>Extra environment variables from the zip's <c>robot.json</c>, as JSON; null when it has none.</summary>
    public string? EntornoJson { get; set; }

    /// <summary>Who uploaded it (their e-mail or name), for the history.</summary>
    public string? SubidaPor { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
