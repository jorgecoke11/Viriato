namespace Viriato.Rpa.Template;

public sealed class RpaClientOptions
{
    public required string BaseUrl { get; init; }

    public required string ApiKey { get; init; }

    /// <summary>Purely for the robot's own logs/diagnostics — the API key alone already identifies
    /// the Despliegue (and therefore the Equipo and Rpa) server-side; this is never sent anywhere.</summary>
    public string? EquipoId { get; init; }
}
