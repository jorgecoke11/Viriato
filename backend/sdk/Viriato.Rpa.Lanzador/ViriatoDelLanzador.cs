using System.Net;
using System.Net.Http.Json;
using Viariato.ApiContracts;

namespace Viriato.Rpa.Lanzador;

/// <summary>Viriato does not know the key: it was regenerated, or it is the key of another Despliegue's environment.</summary>
public sealed class ClaveRechazadaException() : Exception("Viriato rechazó la clave del robot.");

/// <summary>The two things the launcher asks Viriato, always on behalf of one robot, with that robot's key.</summary>
public interface IViriatoDelLanzador
{
    /// <summary>What waits in the robot's queue and what its copies hold.</summary>
    Task<TrabajoDelDespliegueDto> TrabajoAsync(string clave, CancellationToken ct);

    /// <summary>Tells Viriato what the launcher is doing about the robot, for the machine's screen.</summary>
    Task ReportarAsync(string clave, EstadoDelLanzadorRequest estado, CancellationToken ct);
}

public sealed class ViriatoDelLanzador(HttpClient http) : IViriatoDelLanzador
{
    public const string CabeceraDeClave = "X-Api-Key";

    public async Task<TrabajoDelDespliegueDto> TrabajoAsync(string clave, CancellationToken ct)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Get, "api/v1/rpa/lanzador/trabajo", clave, null, ct).ConfigureAwait(false);
        return await respuesta.Content.ReadFromJsonAsync<TrabajoDelDespliegueDto>(ct).ConfigureAwait(false)
            ?? throw new HttpRequestException("Viriato contestó sin datos.");
    }

    public async Task ReportarAsync(string clave, EstadoDelLanzadorRequest estado, CancellationToken ct)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Put, "api/v1/rpa/lanzador/estado", clave, JsonContent.Create(estado), ct).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, string clave, HttpContent? cuerpo, CancellationToken ct)
    {
        using var peticion = new HttpRequestMessage(metodo, ruta) { Content = cuerpo };
        peticion.Headers.Add(CabeceraDeClave, clave);
        // Not the robot itself: Viriato must not take this call as the robot being connected.
        peticion.Headers.Add(RpaHeaders.Lanzador, "1");

        var respuesta = await http.SendAsync(peticion, ct).ConfigureAwait(false);
        if (respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            respuesta.Dispose();
            throw new ClaveRechazadaException();
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var codigo = (int)respuesta.StatusCode;
            respuesta.Dispose();
            throw new HttpRequestException($"Viriato contestó {codigo} a {metodo} {ruta}.");
        }

        return respuesta;
    }
}
