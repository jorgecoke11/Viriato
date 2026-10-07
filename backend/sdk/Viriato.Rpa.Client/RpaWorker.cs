using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Viariato.ApiContracts;

namespace Viriato.Rpa.Client;

public sealed class RpaWorkerOptions
{
    /// <summary>How long to wait when the Despliegue is switched off or the queue is empty.</summary>
    public TimeSpan IntervaloPolling { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>How long to wait after a transient failure (network down, server error) before polling again.</summary>
    public TimeSpan EsperaTrasError { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Extra attempts to report a step's outcome after a transient failure. The work is already
    /// done by then, so losing the report would leave the step claimed forever — worth insisting on.</summary>
    public int ReintentosAlReportar { get; set; } = 3;

    /// <summary>Pause between those reporting attempts (multiplied by the attempt number).</summary>
    public TimeSpan EsperaEntreReintentos { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>When a handler throws, attach the exception (type, message, stack) to the step as evidence
    /// before reporting it failed, so whoever reprocesses it can see what happened without the robot's logs.
    /// A handler that returns <see cref="RpaStepResult.Fallido"/> on purpose already said why.</summary>
    public bool AdjuntarDetalleDeError { get; set; } = true;
}

/// <summary>
/// The reusable robot loop: while the Despliegue is switched on, keep claiming steps off its queue and
/// hand each to <c>handler</c>; whatever the handler returns (or throws) is reported back. Runs until
/// the token is cancelled, then returns normally. A step interrupted by that cancellation is reported
/// as failed so it can be reprocessed — a claimed step is never handed out again on its own.
/// </summary>
public sealed class RpaWorker
{
    private readonly IRpaClient _client;
    private readonly RpaWorkerOptions _options;
    private readonly ILogger _logger;

    public RpaWorker(IRpaClient client, RpaWorkerOptions? options = null, ILogger<RpaWorker>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? new RpaWorkerOptions();
        _logger = logger ?? NullLogger<RpaWorker>.Instance;
    }

    public async Task RunAsync(Func<RpaStepContext, CancellationToken, Task<RpaStepResult>> handler, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!(await _client.ObtenerEstadoAsync(ct).ConfigureAwait(false)).Encendido)
                {
                    await Task.Delay(_options.IntervaloPolling, ct).ConfigureAwait(false);
                    continue;
                }

                var ejecucion = await _client.ObtenerSiguienteEjecucionAsync(ct).ConfigureAwait(false);
                if (ejecucion is null)
                {
                    await Task.Delay(_options.IntervaloPolling, ct).ConfigureAwait(false);
                    continue;
                }

                await ProcesarAsync(ejecucion, handler, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (EsTransitorio(ex))
            {
                _logger.LogWarning(ex, "Fallo temporal hablando con Viriato; se reintenta en {Espera}.", _options.EsperaTrasError);
                try
                {
                    await Task.Delay(_options.EsperaTrasError, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task ProcesarAsync(
        EjecucionAsignadaDto ejecucion,
        Func<RpaStepContext, CancellationToken, Task<RpaStepResult>> handler,
        CancellationToken ct)
    {
        _logger.LogInformation("Paso {Paso} reclamado (caso \"{Caso}\").", ejecucion.EjecucionPasoId, ejecucion.CasoTitulo);

        RpaStepResult resultado;
        try
        {
            resultado = await handler(new RpaStepContext(_client, ejecucion), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await ReportarAsync(ejecucion, RpaStepResult.Fallido("El robot se detuvo antes de terminar el paso.")).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "El paso {Paso} falló.", ejecucion.EjecucionPasoId);
            if (_options.AdjuntarDetalleDeError)
            {
                await AdjuntarDetalleDeErrorAsync(ejecucion, ex).ConfigureAwait(false);
            }

            resultado = RpaStepResult.Fallido(ex.Message);
        }

        await ReportarAsync(ejecucion, resultado).ConfigureAwait(false);
    }

    // Best effort: the failure being reported must not be replaced by a failure to attach its detail.
    private async Task AdjuntarDetalleDeErrorAsync(EjecucionAsignadaDto ejecucion, Exception ex)
    {
        try
        {
            await _client.AgregarEvidenciaAsync(
                ejecucion.EjecucionPasoId, EvidenciaTipo.Otro, "Detalle del error", JsonSerializer.Serialize(ex.ToString())).ConfigureAwait(false);
        }
        catch (Exception detalle)
        {
            _logger.LogWarning(detalle, "No se pudo adjuntar el detalle del error del paso {Paso}.", ejecucion.EjecucionPasoId);
        }
    }

    // Deliberately not tied to the worker's cancellation token: by the time we report, the work is
    // done (or has been interrupted) and the report is what releases the step.
    private async Task ReportarAsync(EjecucionAsignadaDto ejecucion, RpaStepResult resultado)
    {
        var pasoId = ejecucion.EjecucionPasoId;

        for (var intento = 0; ; intento++)
        {
            try
            {
                await (resultado.Outcome switch
                {
                    RpaStepOutcome.Completado => _client.CompletarPasoAsync(pasoId, resultado.ParametrosSalida),
                    RpaStepOutcome.CasoCompletado => _client.CompletarCasoAsync(pasoId, resultado.ParametrosSalida),
                    _ => _client.FallarPasoAsync(pasoId, resultado.Error ?? "El paso falló."),
                }).ConfigureAwait(false);

                _logger.LogInformation("Paso {Paso} reportado como {Resultado}.", pasoId, resultado.Outcome);
                return;
            }
            catch (ViriatoApiException ex) when (ex.IsConflict)
            {
                // "Ya no está en progreso": an earlier attempt whose response was lost already went through.
                _logger.LogWarning("El paso {Paso} ya no estaba en progreso al reportarlo; se da por reportado.", pasoId);
                return;
            }
            catch (Exception ex) when (EsTransitorio(ex) && intento < _options.ReintentosAlReportar)
            {
                _logger.LogWarning(ex, "No se pudo reportar el paso {Paso} (intento {Intento}); se reintenta.", pasoId, intento + 1);
                await Task.Delay(_options.EsperaEntreReintentos * (intento + 1)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo reportar el paso {Paso}; queda reclamado en Viriato.", pasoId);
                return;
            }
        }
    }

    // A conflict while polling means "switched off in the meantime" or a lost claim race — both
    // resolve themselves. Any other client error (bad key, unknown despliegue) is a misconfiguration
    // the operator has to see, so it is allowed to escape and stop the worker.
    private static bool EsTransitorio(Exception ex) => ex switch
    {
        ViriatoApiException api => api.IsTransient || api.IsConflict,
        HttpRequestException => true,
        TaskCanceledException => true,
        _ => false,
    };
}
