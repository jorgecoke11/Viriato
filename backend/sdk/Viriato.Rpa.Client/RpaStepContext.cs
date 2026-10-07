using Viariato.ApiContracts;

namespace Viriato.Rpa.Client;

/// <summary>What a handler gets while running one claimed step: the step itself, and the reporting
/// calls that only make sense for it, already bound to its id.</summary>
public sealed class RpaStepContext
{
    private readonly IRpaClient _client;

    /// <summary>Public so a robot can build one in its own unit tests; at runtime <see cref="RpaWorker"/> does.</summary>
    public RpaStepContext(IRpaClient client, EjecucionAsignadaDto ejecucion)
    {
        _client = client;
        Ejecucion = ejecucion;
    }

    public EjecucionAsignadaDto Ejecucion { get; }

    /// <inheritdoc cref="IRpaClient.ObtenerParametrosAsync"/>
    public Task<ParametrosProceso> ObtenerParametrosAsync(CancellationToken ct = default) =>
        _client.ObtenerParametrosAsync(ct);

    /// <inheritdoc cref="IRpaClient.ObtenerCredencialAsync"/>
    public Task<CredencialRobotDto> ObtenerCredencialAsync(string nombre, CancellationToken ct = default) =>
        _client.ObtenerCredencialAsync(nombre, ct);

    /// <inheritdoc cref="IRpaClient.CrearCasoAsync"/>
    public Task<CasoCreadoDto> CrearCasoAsync(CrearCasoRobotRequest request, CancellationToken ct = default) =>
        _client.CrearCasoAsync(request, ct);

    /// <inheritdoc cref="IRpaClient.AgregarEvidenciaAsync"/>
    public Task AgregarEvidenciaAsync(
        EvidenciaTipo tipo,
        string titulo,
        string? contenidoJson = null,
        Stream? archivo = null,
        string? nombreArchivo = null,
        CancellationToken ct = default) =>
        _client.AgregarEvidenciaAsync(Ejecucion.EjecucionPasoId, tipo, titulo, contenidoJson, archivo, nombreArchivo, ct);

    /// <inheritdoc cref="IRpaClient.CambiarEstadoCasoAsync"/>
    public Task CambiarEstadoCasoAsync(string codigoEstado, CancellationToken ct = default) =>
        _client.CambiarEstadoCasoAsync(Ejecucion.EjecucionPasoId, codigoEstado, ct);

    /// <summary>
    /// Like <see cref="CambiarEstadoCasoAsync"/>, but returns false instead of throwing when Viriato says
    /// there is no such state (the Flujo does not define it). A business state is informational: a step
    /// should not fail over one. Any other error — bad key, network, server — still throws.
    /// </summary>
    public async Task<bool> IntentarCambiarEstadoCasoAsync(string codigoEstado, CancellationToken ct = default)
    {
        try
        {
            await _client.CambiarEstadoCasoAsync(Ejecucion.EjecucionPasoId, codigoEstado, ct).ConfigureAwait(false);
            return true;
        }
        catch (ViriatoApiException ex) when (ex.IsNotFound || ex.IsConflict)
        {
            return false;
        }
    }
}
