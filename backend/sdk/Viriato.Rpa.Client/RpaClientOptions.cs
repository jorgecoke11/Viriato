namespace Viriato.Rpa.Client;

public sealed class RpaClientOptions
{
    /// <summary>Base address of the Viriato API, e.g. <c>https://viriato.tuempresa.com</c>. A path prefix
    /// (a reverse proxy mounting the API under <c>/viriato</c>) is respected.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>The Despliegue's API key — it alone identifies the Despliegue (and therefore the Equipo,
    /// Servicio and Flujo) on the server.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Tells this running copy of the robot apart from the others of the same Despliegue (replicas of a container, more
    /// processes started with the same API key), so each one is its own slot: a copy stuck on a case does not stop the rest
    /// from taking the next ones. Leave it empty: the client then makes one up when the process starts, which is what you want.
    /// Set it only to keep a stable id, or when one process runs several robots.
    /// </summary>
    public string? InstanciaId { get; set; }

    /// <summary>Per-request timeout. Generous by default because evidence uploads (videos) can be large.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Everything wrong with this configuration, in words a person setting up a robot can act on.
    /// Empty means it is usable.</summary>
    public IReadOnlyList<string> Validar()
    {
        var problemas = new List<string>();

        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            problemas.Add("Viriato:BaseUrl (variable Viriato__BaseUrl) — dirección de la API de Viriato.");
        }
        else if (!TryParseBaseAddress(out _))
        {
            problemas.Add($"Viriato:BaseUrl no es una URL http(s) válida: '{BaseUrl}'.");
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            problemas.Add("Viriato:ApiKey (variable Viriato__ApiKey) — clave del Despliegue de este robot.");
        }

        return problemas;
    }

    /// <summary>The id made up for this process, the same for every client created in it.</summary>
    private static readonly string InstanciaDelProceso = Guid.NewGuid().ToString("N");

    /// <summary>What this copy sends as its id: the configured one, or the one of the process.</summary>
    internal string ResolveInstanciaId() => string.IsNullOrWhiteSpace(InstanciaId) ? InstanciaDelProceso : InstanciaId.Trim();

    internal Uri ResolveBaseAddress()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            throw new ArgumentException("BaseUrl es obligatorio.", nameof(BaseUrl));
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new ArgumentException("ApiKey es obligatoria.", nameof(ApiKey));
        }

        return TryParseBaseAddress(out var baseAddress)
            ? baseAddress
            : throw new ArgumentException($"BaseUrl no es una URL http(s) válida: '{BaseUrl}'.", nameof(BaseUrl));
    }

    private bool TryParseBaseAddress(out Uri baseAddress) =>
        Uri.TryCreate(BaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out baseAddress!)
        && (baseAddress.Scheme == Uri.UriSchemeHttp || baseAddress.Scheme == Uri.UriSchemeHttps);
}
