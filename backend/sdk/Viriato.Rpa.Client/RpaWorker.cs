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

    /// <summary>While a step runs, how often the Despliegue's settings (<see cref="IAjusteDelDespliegue"/>) are read again, so a change
    /// made in Viriato — the live view switched off on a slow robot — does not wait for the step to end. Only with settings registered.</summary>
    public TimeSpan IntervaloAjustesDuranteUnPaso { get; set; } = TimeSpan.FromSeconds(15);
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
    private readonly IReadOnlyList<IAjusteDelDespliegue> _ajustes;
    private DespliegueEstadoDto? _ultimoEstado;

    /// <param name="ajustes">What follows the Despliegue's settings on this machine (the live view…), given its state on every read.</param>
    public RpaWorker(
        IRpaClient client, RpaWorkerOptions? options = null, ILogger<RpaWorker>? logger = null, IEnumerable<IAjusteDelDespliegue>? ajustes = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? new RpaWorkerOptions();
        _logger = logger ?? NullLogger<RpaWorker>.Instance;
        _ajustes = ajustes?.ToList() ?? [];
    }

    public async Task RunAsync(Func<RpaStepContext, CancellationToken, Task<RpaStepResult>> handler, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(handler);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var estado = await _client.ObtenerEstadoAsync(ct).ConfigureAwait(false);
                _ultimoEstado = estado;
                await AplicarAjustesAsync(estado, ct).ConfigureAwait(false);
                if (!estado.Encendido)
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
        using var finDelPaso = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var ajustes = _ajustes.Count > 0 ? SeguirAjustesAsync(finDelPaso.Token) : Task.CompletedTask;
        try
        {
            resultado = await handler(new RpaStepContext(_client, ejecucion, _ultimoEstado), ct).ConfigureAwait(false);
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
        finally
        {
            await finDelPaso.CancelAsync().ConfigureAwait(false);
            await ajustes.ConfigureAwait(false);
        }

        await ReportarAsync(ejecucion, resultado).ConfigureAwait(false);
    }

    // Keeps the settings up to date while a step runs. Best effort: a failed read just waits for the next one.
    private async Task SeguirAjustesAsync(CancellationToken finDelPaso)
    {
        while (!finDelPaso.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.IntervaloAjustesDuranteUnPaso, finDelPaso).ConfigureAwait(false);
                await AplicarAjustesAsync(await _client.ObtenerEstadoAsync(finDelPaso).ConfigureAwait(false), finDelPaso).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (finDelPaso.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Whatever it is, the step must not pay for it; the main loop deals with a bad key or a switched-off Despliegue.
                _logger.LogDebug(ex, "No se pudieron releer los ajustes del despliegue durante el paso.");
            }
        }
    }

    private async Task AplicarAjustesAsync(DespliegueEstadoDto estado, CancellationToken ct)
    {
        foreach (var ajuste in _ajustes)
        {
            try
            {
                await ajuste.AplicarAsync(estado, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "No se pudo aplicar {Ajuste}; el robot sigue trabajando.", ajuste.GetType().Name);
            }
        }
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
