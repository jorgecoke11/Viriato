using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Viariato.ApiContracts;
using Viriato.Rpa.Client;

namespace Viriato.Rpa.Client.Tests;

public sealed class RpaWorkerTests
{
    private static readonly TimeSpan Instant = TimeSpan.FromMilliseconds(1);

    private static RpaWorkerOptions FastOptions() => new()
    {
        IntervaloPolling = Instant,
        EsperaTrasError = Instant,
        EsperaEntreReintentos = Instant,
        ReintentosAlReportar = 2,
    };

    private static EjecucionAsignadaDto NuevaEjecucion() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Caso 1", "Proceso Alta", "Portal", null, null);

    /// <summary>In-memory queue + recorder; each scripted call can throw instead of returning.</summary>
    private sealed class FakeRpaClient : IRpaClient
    {
        public Queue<Func<Task<DespliegueEstadoDto>>> Estados { get; } = new();
        public Queue<Func<Task<EjecucionAsignadaDto?>>> Claims { get; } = new();
        public List<string> Calls { get; } = [];
        public Func<Task>? OnReport { get; set; }
        public Func<Task>? OnEvidencia { get; set; }
        public CancellationTokenSource? StopWhenQueueDrains { get; set; }

        public Task<DespliegueEstadoDto> ObtenerEstadoAsync(CancellationToken ct = default) =>
            Estados.Count > 0 ? Estados.Dequeue()() : Task.FromResult(Encendido(true));

        public async Task<bool> IsActivoAsync(CancellationToken ct = default) => (await ObtenerEstadoAsync(ct)).Encendido;

        public Task<EjecucionAsignadaDto?> ObtenerSiguienteEjecucionAsync(CancellationToken ct = default)
        {
            Calls.Add("claim");
            if (Claims.Count > 0) return Claims.Dequeue()();

            // Nothing scripted left: the scenario is over, so end the worker's loop.
            StopWhenQueueDrains?.Cancel();
            return Task.FromResult<EjecucionAsignadaDto?>(null);
        }

        public Task CompletarPasoAsync(Guid id, string? parametrosSalida = null, CancellationToken ct = default) => Report($"completar:{parametrosSalida}");

        public Task CompletarCasoAsync(Guid id, string? parametrosSalida = null, CancellationToken ct = default) => Report($"completar-caso:{parametrosSalida}");

        public Task FallarPasoAsync(Guid id, string error, CancellationToken ct = default) => Report($"fallar:{error}");

        public Task CambiarEstadoCasoAsync(Guid id, string codigoEstado, CancellationToken ct = default) => Report($"estado:{codigoEstado}");

        public Task<CredencialRobotDto> ObtenerCredencialAsync(string nombre, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<ParametrosProceso> ObtenerParametrosAsync(CancellationToken ct = default) => throw new NotSupportedException();

        public Task<CasoCreadoDto> CrearCasoAsync(CrearCasoRobotRequest request, CancellationToken ct = default) => throw new NotSupportedException();

        public async Task AgregarEvidenciaAsync(Guid id, EvidenciaTipo tipo, string titulo, string? contenidoJson = null, Stream? archivo = null, string? nombreArchivo = null, CancellationToken ct = default)
        {
            if (OnEvidencia is not null) await OnEvidencia();
            await Report($"evidencia:{tipo}:{titulo}");
        }

        private Task Report(string call)
        {
            Calls.Add(call);
            return OnReport?.Invoke() ?? Task.CompletedTask;
        }
    }

    private static DespliegueEstadoDto Encendido(bool encendido) => new(encendido, "PC-01", "Robot", "Proceso");

    private static (RpaWorker Worker, FakeRpaClient Client, CancellationTokenSource Cts) Create(params EjecucionAsignadaDto[] queue)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var client = new FakeRpaClient { StopWhenQueueDrains = cts };
        foreach (var ejecucion in queue)
        {
            client.Claims.Enqueue(() => Task.FromResult<EjecucionAsignadaDto?>(ejecucion));
        }

        return (new RpaWorker(client, FastOptions()), client, cts);
    }

    [Fact]
    public async Task AHandlerThatCompletes_ReportsTheStepCompletedWithItsOutput()
    {
        var (worker, client, cts) = Create(NuevaEjecucion());

        await worker.RunAsync((_, _) => Task.FromResult(RpaStepResult.Completado("{\"ok\":true}")), cts.Token);

        Assert.Contains("completar:{\"ok\":true}", client.Calls);
        Assert.DoesNotContain(client.Calls, c => c.StartsWith("fallar"));
    }

    [Fact]
    public async Task AHandlerThatClosesTheCaso_ReportsCompletarCasoNotCompletarPaso()
    {
        var (worker, client, cts) = Create(NuevaEjecucion());

        await worker.RunAsync((_, _) => Task.FromResult(RpaStepResult.CasoCompletado("fin")), cts.Token);

        Assert.Contains("completar-caso:fin", client.Calls);
        Assert.DoesNotContain(client.Calls, c => c.StartsWith("completar:"));
    }

    [Fact]
    public async Task AHandlerThatThrows_ReportsTheStepFailedWithTheMessage()
    {
        var (worker, client, cts) = Create(NuevaEjecucion());

        await worker.RunAsync((_, _) => throw new InvalidOperationException("el portal no responde"), cts.Token);

        Assert.Contains("fallar:el portal no responde", client.Calls);
    }

    [Fact]
    public async Task AHandlerThatThrows_HasTheExceptionAttachedAsEvidenceBeforeTheFailureIsReported()
    {
        var (worker, client, cts) = Create(NuevaEjecucion());

        await worker.RunAsync((_, _) => throw new InvalidOperationException("el portal no responde"), cts.Token);

        var evidencia = client.Calls.IndexOf("evidencia:Otro:Detalle del error");
        var fallo = client.Calls.FindIndex(c => c.StartsWith("fallar:"));
        Assert.True(evidencia >= 0, "the exception detail must be attached");
        Assert.True(evidencia < fallo, "and attached before the step is reported failed");
    }

    [Fact]
    public async Task TheErrorDetailCanBeSwitchedOff_AndIsNotAttachedForADeliberateFallido()
    {
        var (_, client, cts) = Create(NuevaEjecucion(), NuevaEjecucion());
        var opciones = FastOptions();
        opciones.AdjuntarDetalleDeError = false;
        var sinDetalle = new RpaWorker(client, opciones);
        var llamada = 0;

        await sinDetalle.RunAsync((_, _) =>
            ++llamada == 1
                ? throw new InvalidOperationException("boom")
                : Task.FromResult(RpaStepResult.Fallido("datos incompletos")), cts.Token);

        Assert.DoesNotContain(client.Calls, c => c.StartsWith("evidencia"));
        Assert.Contains("fallar:boom", client.Calls);
        Assert.Contains("fallar:datos incompletos", client.Calls);
    }

    [Fact]
    public async Task AFailureToAttachTheErrorDetail_DoesNotPreventReportingTheFailure()
    {
        var (worker, client, cts) = Create(NuevaEjecucion());
        client.OnEvidencia = () => throw new HttpRequestException("se cortó");

        await worker.RunAsync((_, _) => throw new InvalidOperationException("boom"), cts.Token);

        Assert.Contains("fallar:boom", client.Calls);
    }

    [Fact]
    public async Task AHandlerThatReturnsFallido_ReportsTheStepFailed()
    {
        var (worker, client, cts) = Create(NuevaEjecucion());

        await worker.RunAsync((_, _) => Task.FromResult(RpaStepResult.Fallido("datos incompletos")), cts.Token);

        Assert.Contains("fallar:datos incompletos", client.Calls);
    }

    [Fact]
    public async Task TheContext_ExposesTheStepAndBindsTheReportingCallsToIt()
    {
        var ejecucion = NuevaEjecucion();
        var (worker, client, cts) = Create(ejecucion);
        EjecucionAsignadaDto? visto = null;

        await worker.RunAsync(async (paso, ct) =>
        {
            visto = paso.Ejecucion;
            await paso.AgregarEvidenciaAsync(EvidenciaTipo.Screenshot, "captura", ct: ct);
            await paso.CambiarEstadoCasoAsync("EN_REVISION", ct);
            return RpaStepResult.Completado();
        }, cts.Token);

        Assert.Equal(ejecucion, visto);
        Assert.Contains("evidencia:Screenshot:captura", client.Calls);
        Assert.Contains("estado:EN_REVISION", client.Calls);
    }

    [Fact]
    public async Task WhenTheDespliegueIsSwitchedOff_NothingIsClaimed()
    {
        var (worker, client, cts) = Create(NuevaEjecucion());
        client.Estados.Enqueue(() => Task.FromResult(Encendido(false)));
        client.Estados.Enqueue(() => Task.FromResult(Encendido(false)));
        var handled = 0;
        // Stop after the two "off" polls — if the worker ignored them it would have claimed by then.
        client.Estados.Enqueue(() =>
        {
            cts.Cancel();
            return Task.FromResult(Encendido(false));
        });

        await worker.RunAsync((_, _) =>
        {
            handled++;
            return Task.FromResult(RpaStepResult.Completado());
        }, cts.Token);

        Assert.Equal(0, handled);
        Assert.DoesNotContain("claim", client.Calls);
    }

    [Fact]
    public async Task ATransientNetworkFailureWhilePolling_IsSurvivedAndTheNextStepStillRuns()
    {
        var (worker, client, cts) = Create();
        client.Claims.Enqueue(() => throw new HttpRequestException("sin red"));
        client.Claims.Enqueue(() => throw new ViriatoApiException(HttpStatusCode.ServiceUnavailable, null));
        client.Claims.Enqueue(() => Task.FromResult<EjecucionAsignadaDto?>(NuevaEjecucion()));

        await worker.RunAsync((_, _) => Task.FromResult(RpaStepResult.Completado("ok")), cts.Token);

        Assert.Contains("completar:ok", client.Calls);
    }

    [Fact]
    public async Task AConflictWhileClaiming_MeaningSwitchedOffInTheMeantime_IsNotFatal()
    {
        var (worker, client, cts) = Create();
        client.Claims.Enqueue(() => throw new ViriatoApiException(HttpStatusCode.Conflict, "El despliegue está apagado."));

        await worker.RunAsync((_, _) => Task.FromResult(RpaStepResult.Completado()), cts.Token);

        Assert.Equal(2, client.Calls.Count(c => c == "claim"));
    }

    [Fact]
    public async Task ABadApiKey_StopsTheWorkerWithTheError()
    {
        var (worker, client, cts) = Create();
        client.Estados.Enqueue(() => throw new ViriatoApiException(HttpStatusCode.Unauthorized, null));

        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() =>
            worker.RunAsync((_, _) => Task.FromResult(RpaStepResult.Completado()), cts.Token));

        Assert.True(ex.IsUnauthorized);
    }

    [Fact]
    public async Task AFailedReport_IsRetriedUntilItGoesThrough()
    {
        var (worker, client, cts) = Create(NuevaEjecucion());
        var attempts = 0;
        client.OnReport = () => ++attempts < 3 ? throw new HttpRequestException("se cortó") : Task.CompletedTask;

        await worker.RunAsync((_, _) => Task.FromResult(RpaStepResult.Completado()), cts.Token);

        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task AConflictWhileReporting_MeansAnEarlierAttemptAlreadyWentThrough_SoItIsNotRetried()
    {
        var (worker, client, cts) = Create(NuevaEjecucion());
        var attempts = 0;
        client.OnReport = () =>
        {
            attempts++;
            throw new ViriatoApiException(HttpStatusCode.Conflict, "Este paso ya no está en progreso.");
        };

        await worker.RunAsync((_, _) => Task.FromResult(RpaStepResult.Completado()), cts.Token);

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task StoppingTheWorkerMidStep_ReportsTheStepFailedSoItCanBeReprocessed()
    {
        var (worker, client, cts) = Create(NuevaEjecucion());

        await worker.RunAsync(async (_, ct) =>
        {
            cts.Cancel();
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return RpaStepResult.Completado();
        }, cts.Token);

        Assert.Contains(client.Calls, c => c.StartsWith("fallar:El robot se detuvo"));
        Assert.DoesNotContain(client.Calls, c => c.StartsWith("completar"));
    }

    [Fact]
    public async Task AddViriatoRpaClient_RegistersAWorkerAndAClientThatCanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddViriatoRpaClient(
            o =>
            {
                o.BaseUrl = "http://viriato.test";
                o.ApiKey = "clave";
            },
            w => w.IntervaloPolling = TimeSpan.FromSeconds(7));

        await using var provider = services.BuildServiceProvider();

        Assert.IsType<RpaClient>(provider.GetRequiredService<IRpaClient>());
        Assert.NotNull(provider.GetRequiredService<RpaWorker>());
    }

    [Fact]
    public async Task AddViriatoRpaClient_FailsOnResolveWhenTheApiKeyIsMissing()
    {
        var services = new ServiceCollection();
        services.AddViriatoRpaClient(o => o.BaseUrl = "http://viriato.test");

        await using var provider = services.BuildServiceProvider();

        Assert.Throws<ArgumentException>(() => provider.GetRequiredService<IRpaClient>());
    }
}
