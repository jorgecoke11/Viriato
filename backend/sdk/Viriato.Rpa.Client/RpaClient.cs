using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Viariato.ApiContracts;

namespace Viriato.Rpa.Client;

/// <inheritdoc cref="IRpaClient"/>
public sealed class RpaClient : IRpaClient, IDisposable
{
    private const string ApiKeyHeader = "X-Api-Key";
    private const string RpaRoute = "api/v1/rpa";

    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;

    /// <summary>Standalone use (a plain console robot, no DI): builds and owns its own HttpClient.</summary>
    public RpaClient(RpaClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _http = new HttpClient { BaseAddress = options.ResolveBaseAddress(), Timeout = options.Timeout };
        _http.DefaultRequestHeaders.Add(ApiKeyHeader, options.ApiKey);
        _http.DefaultRequestHeaders.Add(RpaHeaders.Instancia, options.ResolveInstanciaId());
        _ownsHttpClient = true;
    }

    /// <summary>DI use: the HttpClient arrives already configured (base address, API key) by
    /// <see cref="ServiceCollectionExtensions.AddViriatoRpaClient"/>, and its lifetime belongs to the factory.</summary>
    [ActivatorUtilitiesConstructor]
    public RpaClient(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<DespliegueEstadoDto> ObtenerEstadoAsync(CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"{RpaRoute}/despliegue", ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<DespliegueEstadoDto>(ct).ConfigureAwait(false))!;
    }

    public async Task<bool> IsActivoAsync(CancellationToken ct = default) =>
        (await ObtenerEstadoAsync(ct).ConfigureAwait(false)).Encendido;

    public async Task<EjecucionAsignadaDto?> ObtenerSiguienteEjecucionAsync(CancellationToken ct = default)
    {
        using var response = await _http.PostAsync($"{RpaRoute}/cola/siguiente", content: null, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<EjecucionAsignadaDto>(ct).ConfigureAwait(false);
    }

    public async Task<ParametrosProceso> ObtenerParametrosAsync(CancellationToken ct = default)
    {
        using var response = await _http.GetAsync($"{RpaRoute}/parametros", ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
        var valores = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(ct).ConfigureAwait(false);
        return new ParametrosProceso(valores ?? []);
    }

    public async Task<CredencialRobotDto> ObtenerCredencialAsync(string nombre, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);

        using var response = await _http.GetAsync($"{RpaRoute}/credenciales/{Uri.EscapeDataString(nombre)}", ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<CredencialRobotDto>(ct).ConfigureAwait(false))!;
    }

    public async Task<CasoCreadoDto> CrearCasoAsync(CrearCasoRobotRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await _http.PostAsJsonAsync($"{RpaRoute}/casos", request, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<CasoCreadoDto>(ct).ConfigureAwait(false))!;
    }

    public Task CompletarPasoAsync(Guid ejecucionPasoId, string? parametrosSalida = null, CancellationToken ct = default) =>
        PostJsonAsync($"{RpaRoute}/pasos/{ejecucionPasoId}/completar", new CompletarPasoRequest(parametrosSalida), ct);

    public Task CompletarCasoAsync(Guid ejecucionPasoId, string? parametrosSalida = null, CancellationToken ct = default) =>
        PostJsonAsync($"{RpaRoute}/pasos/{ejecucionPasoId}/completar-caso", new CompletarPasoRequest(parametrosSalida), ct);

    public Task FallarPasoAsync(Guid ejecucionPasoId, string error, CancellationToken ct = default) =>
        PostJsonAsync($"{RpaRoute}/pasos/{ejecucionPasoId}/fallar", new FallarPasoRequest(error), ct);

    public Task CambiarEstadoCasoAsync(Guid ejecucionPasoId, string codigoEstado, CancellationToken ct = default) =>
        PostJsonAsync($"{RpaRoute}/pasos/{ejecucionPasoId}/estado-negocio", new CambiarEstadoNegocioRequest(codigoEstado), ct);

    public Task ReportarEnVivoAsync(Guid ejecucionPasoId, int? porcentaje = null, string? mensaje = null, string? vistaUrl = null, CancellationToken ct = default) =>
        PostJsonAsync($"{RpaRoute}/pasos/{ejecucionPasoId}/en-vivo", new ReportarEnVivoRequest(porcentaje, mensaje, vistaUrl), ct);

    public async Task AgregarEvidenciaAsync(
        Guid ejecucionPasoId,
        EvidenciaTipo tipo,
        string titulo,
        string? contenidoJson = null,
        Stream? archivo = null,
        string? nombreArchivo = null,
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(tipo.ToString()), "tipo" },
            { new StringContent(titulo), "titulo" },
        };
        if (contenidoJson is not null)
        {
            form.Add(new StringContent(contenidoJson), "contenidoJson");
        }

        if (archivo is not null)
        {
            form.Add(new StreamContent(archivo), "file", nombreArchivo ?? "evidencia");
        }

        using var response = await _http.PostAsync($"{RpaRoute}/pasos/{ejecucionPasoId}/evidencias", form, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }

    private async Task PostJsonAsync<TBody>(string route, TBody body, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync(route, body, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw new ViriatoApiException(response.StatusCode, await ReadProblemDetailAsync(response, ct).ConfigureAwait(false));
    }

    // The API reports errors as RFC 7807 problem details; anything else (a proxy's HTML error page)
    // is not worth surfacing as-is, so the status code alone will have to do.
    private static async Task<string?> ReadProblemDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
            return problem.ValueKind == JsonValueKind.Object && problem.TryGetProperty("detail", out var detail)
                ? detail.GetString()
                : null;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }
}
