using System.Net;
using System.Net.Http.Json;
using Viariato.ApiContracts;

namespace Viriato.Rpa.Template;

/// <summary>
/// Everything a robot needs to talk to Viriato: check whether its Despliegue is switched on, pull
/// the next pending execution off its queue, and report back — complete, fail, attach evidence, or
/// change the owning Caso's business state. Every write is scoped to the EjecucionPasoId handed back
/// by <see cref="ObtenerSiguienteEjecucionAsync"/>; the server rejects anything the caller didn't
/// legitimately claim.
/// </summary>
public sealed class RpaClient : IDisposable
{
    private readonly HttpClient _http;

    public RpaClient(RpaClientOptions options)
    {
        _http = new HttpClient { BaseAddress = new Uri(options.BaseUrl) };
        _http.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
    }

    public async Task<DespliegueEstadoDto> ObtenerEstadoAsync(CancellationToken ct = default)
    {
        var response = await _http.GetAsync("/api/v1/rpa/despliegue", ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DespliegueEstadoDto>(cancellationToken: ct))!;
    }

    public async Task<bool> IsActivoAsync(CancellationToken ct = default) => (await ObtenerEstadoAsync(ct)).Encendido;

    /// <summary>Null means the queue is empty right now — not an error, just nothing to do yet.</summary>
    public async Task<EjecucionAsignadaDto?> ObtenerSiguienteEjecucionAsync(CancellationToken ct = default)
    {
        var response = await _http.PostAsync("/api/v1/rpa/cola/siguiente", content: null, ct);
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EjecucionAsignadaDto>(cancellationToken: ct);
    }

    public async Task CompletarPasoAsync(Guid ejecucionPasoId, string? parametrosSalida = null, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/v1/rpa/pasos/{ejecucionPasoId}/completar", new CompletarPasoRequest(parametrosSalida), ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>For the one step that knows it is the true end of the cycle: completes this paso AND
    /// closes the whole Caso as Completado right now, regardless of whether the Flujo defines more
    /// steps after it. Use <see cref="CompletarPasoAsync"/> instead for a step that is just one link in
    /// the chain — the Caso finishes on its own once there's nothing left to run.</summary>
    public async Task CompletarCasoAsync(Guid ejecucionPasoId, string? parametrosSalida = null, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"/api/v1/rpa/pasos/{ejecucionPasoId}/completar-caso", new CompletarPasoRequest(parametrosSalida), ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task FallarPasoAsync(Guid ejecucionPasoId, string error, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync($"/api/v1/rpa/pasos/{ejecucionPasoId}/fallar", new FallarPasoRequest(error), ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task CambiarEstadoCasoAsync(Guid ejecucionPasoId, string codigoEstado, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync(
            $"/api/v1/rpa/pasos/{ejecucionPasoId}/estado-negocio", new CambiarEstadoNegocioRequest(codigoEstado), ct);
        response.EnsureSuccessStatusCode();
    }

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

        var response = await _http.PostAsync($"/api/v1/rpa/pasos/{ejecucionPasoId}/evidencias", form, ct);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// The reusable "template" shape: while the Despliegue is switched on, keep claiming and running
    /// work. <paramref name="handler"/> does the actual automation and returns the output params to
    /// report (or null); a normal return auto-completes the step, an exception auto-fails it with the
    /// exception's message. Runs until <paramref name="ct"/> is cancelled.
    /// </summary>
    public async Task EjecutarAsync(
        Func<EjecucionAsignadaDto, RpaClient, CancellationToken, Task<string?>> handler,
        TimeSpan intervaloPolling,
        CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            var estado = await ObtenerEstadoAsync(ct);
            if (!estado.Encendido)
            {
                await Task.Delay(intervaloPolling, ct);
                continue;
            }

            var ejecucion = await ObtenerSiguienteEjecucionAsync(ct);
            if (ejecucion is null)
            {
                await Task.Delay(intervaloPolling, ct);
                continue;
            }

            try
            {
                var parametrosSalida = await handler(ejecucion, this, ct);
                await CompletarPasoAsync(ejecucion.EjecucionPasoId, parametrosSalida, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await FallarPasoAsync(ejecucion.EjecucionPasoId, ex.Message, ct);
            }
        }
    }

    public void Dispose() => _http.Dispose();
}
