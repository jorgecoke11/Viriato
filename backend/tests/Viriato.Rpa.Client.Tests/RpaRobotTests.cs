using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Viariato.ApiContracts;
using Viriato.Rpa.Client;
using Viriato.Rpa.Client.Selenium;

namespace Viriato.Rpa.Client.Tests;

public sealed class RpaClientOptionsValidationTests
{
    [Fact]
    public void AnEmptyConfiguration_ReportsBothMissingSettings_NamingTheirEnvironmentVariables()
    {
        var problemas = new RpaClientOptions().Validar();

        Assert.Equal(2, problemas.Count);
        Assert.Contains(problemas, p => p.Contains("Viriato__BaseUrl"));
        Assert.Contains(problemas, p => p.Contains("Viriato__ApiKey"));
    }

    [Theory]
    [InlineData("no-es-una-url")]
    [InlineData("ftp://viriato.test")]
    public void ABaseUrlThatIsNotHttp_IsReported(string url)
    {
        var problemas = new RpaClientOptions { BaseUrl = url, ApiKey = "clave" }.Validar();

        Assert.Single(problemas);
        Assert.Contains("no es una URL http(s) válida", problemas[0]);
    }

    [Fact]
    public void ACompleteConfiguration_HasNoProblems()
    {
        Assert.Empty(new RpaClientOptions { BaseUrl = "https://viriato.test", ApiKey = "clave" }.Validar());
    }
}

public sealed class RpaStepContextTests
{
    private sealed class ClientQueFallaAlCambiarEstado(Exception? error) : IRpaClient
    {
        public Task<DespliegueEstadoDto> ObtenerEstadoAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> IsActivoAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<EjecucionAsignadaDto?> ObtenerSiguienteEjecucionAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task CompletarPasoAsync(Guid id, string? parametrosSalida = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task CompletarCasoAsync(Guid id, string? parametrosSalida = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task FallarPasoAsync(Guid id, string error, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AgregarEvidenciaAsync(Guid id, EvidenciaTipo tipo, string titulo, string? contenidoJson = null, Stream? archivo = null, string? nombreArchivo = null, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<CredencialRobotDto> ObtenerCredencialAsync(string nombre, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<ParametrosProceso> ObtenerParametrosAsync(CancellationToken ct = default) => throw new NotSupportedException();

        public Task<CasoCreadoDto> CrearCasoAsync(CrearCasoRobotRequest request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task CambiarEstadoCasoAsync(Guid id, string codigoEstado, CancellationToken ct = default) =>
            error is null ? Task.CompletedTask : Task.FromException(error);
    }

    private static RpaStepContext Contexto(Exception? error) =>
        new(new ClientQueFallaAlCambiarEstado(error), new EjecucionAsignadaDto(Guid.NewGuid(), Guid.NewGuid(), "Caso", "Flujo", "App", null, null));

    [Fact]
    public async Task IntentarCambiarEstado_ReturnsTrueWhenTheStateIsApplied()
    {
        Assert.True(await Contexto(null).IntentarCambiarEstadoCasoAsync("EN_CURSO"));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)] // the Flujo does not define that state
    [InlineData(HttpStatusCode.Conflict)]
    public async Task IntentarCambiarEstado_ReturnsFalseWhenViriatoRefusesTheState(HttpStatusCode status)
    {
        Assert.False(await Contexto(new ViriatoApiException(status, "Estado de negocio no encontrado.")).IntentarCambiarEstadoCasoAsync("INEXISTENTE"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task IntentarCambiarEstado_DoesNotHideRealFailures(HttpStatusCode status)
    {
        await Assert.ThrowsAsync<ViriatoApiException>(() => Contexto(new ViriatoApiException(status, null)).IntentarCambiarEstadoCasoAsync("EN_CURSO"));
    }
}

public sealed class RpaRobotTests
{
    private static readonly string[] ConfiguracionValida =
    [
        "--Viriato:BaseUrl=http://viriato.test",
        "--Viriato:ApiKey=clave",
        "--Worker:IntervaloPolling=00:00:00.001",
        "--Worker:EsperaTrasError=00:00:00.001",
    ];

    private sealed class FakeHandler : IRpaStepHandler
    {
        public Func<RpaStepContext, Task<RpaStepResult>> Accion { get; set; } = _ => Task.FromResult(RpaStepResult.Completado());

        public Task<RpaStepResult> EjecutarAsync(RpaStepContext paso, CancellationToken ct) => Accion(paso);
    }

    private sealed class FakeViriato : IRpaClient
    {
        public Queue<EjecucionAsignadaDto> Cola { get; } = new();
        public List<string> Llamadas { get; } = [];
        public Exception? ErrorAlConsultar { get; set; }

        public Task<DespliegueEstadoDto> ObtenerEstadoAsync(CancellationToken ct = default) =>
            ErrorAlConsultar is null
                ? Task.FromResult(new DespliegueEstadoDto(true, "Equipo", "Servicio", "Flujo"))
                : Task.FromException<DespliegueEstadoDto>(ErrorAlConsultar);

        public async Task<bool> IsActivoAsync(CancellationToken ct = default) => (await ObtenerEstadoAsync(ct)).Encendido;

        public Task<EjecucionAsignadaDto?> ObtenerSiguienteEjecucionAsync(CancellationToken ct = default) =>
            Task.FromResult(Cola.Count > 0 ? Cola.Dequeue() : null);

        public Task CompletarPasoAsync(Guid id, string? parametrosSalida = null, CancellationToken ct = default)
        {
            Llamadas.Add($"completar:{parametrosSalida}");
            return Task.CompletedTask;
        }

        public Task CompletarCasoAsync(Guid id, string? parametrosSalida = null, CancellationToken ct = default) => Task.CompletedTask;

        public Task FallarPasoAsync(Guid id, string error, CancellationToken ct = default)
        {
            Llamadas.Add($"fallar:{error}");
            return Task.CompletedTask;
        }

        public Task CambiarEstadoCasoAsync(Guid id, string codigoEstado, CancellationToken ct = default) => Task.CompletedTask;

        public Task<CredencialRobotDto> ObtenerCredencialAsync(string nombre, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<ParametrosProceso> ObtenerParametrosAsync(CancellationToken ct = default) => throw new NotSupportedException();

        public Task<CasoCreadoDto> CrearCasoAsync(CrearCasoRobotRequest request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task AgregarEvidenciaAsync(Guid id, EvidenciaTipo tipo, string titulo, string? contenidoJson = null, Stream? archivo = null, string? nombreArchivo = null, CancellationToken ct = default) => Task.CompletedTask;
    }

    // AddRpaHandler<T> needs a type it can construct: this one hands out the shared fake.
    private sealed class HandlerCompartido(FakeHandler inner) : IRpaStepHandler
    {
        public Task<RpaStepResult> EjecutarAsync(RpaStepContext paso, CancellationToken ct) => inner.EjecutarAsync(paso, ct);
    }

    private static EjecucionAsignadaDto NuevaEjecucion() => new(Guid.NewGuid(), Guid.NewGuid(), "Caso 1", "Flujo", "App", null, null);

    private static Microsoft.Extensions.Hosting.HostApplicationBuilder Montar(string[] args, FakeViriato viriato, FakeHandler handler)
    {
        var builder = RpaRobot.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IRpaClient>(viriato);
        builder.Services.AddSingleton(handler);
        builder.Services.AddSingleton(sp => new HandlerCompartido(sp.GetRequiredService<FakeHandler>()));
        builder.AddRpaHandler<HandlerCompartido>();
        return builder;
    }

    [Fact]
    public async Task AnIncompleteConfiguration_ListsEverythingMissing_IncludingTheRobotsOwn_AndStartsNothing()
    {
        var viriato = new FakeViriato();
        var escritor = new StringWriter();
        var builder = Montar([], viriato, new FakeHandler());

        var codigo = await builder.RunRobotAsync(() => ["Tradeplace:Usuario — falta."], escritor);

        Assert.Equal(1, codigo);
        var texto = escritor.ToString();
        Assert.Contains("Viriato__ApiKey", texto);
        Assert.Contains("Viriato__BaseUrl", texto);
        Assert.Contains("Tradeplace:Usuario", texto);
        Assert.Empty(viriato.Llamadas);
    }

    [Fact]
    public async Task ARunningRobot_FeedsTheHandlerFromTheQueue_AndReportsItsResult()
    {
        var viriato = new FakeViriato();
        viriato.Cola.Enqueue(NuevaEjecucion());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var handler = new FakeHandler
        {
            Accion = paso =>
            {
                cts.Cancel(); // one step is enough
                return Task.FromResult(RpaStepResult.Completado($"hecho:{paso.Ejecucion.CasoTitulo}"));
            },
        };

        var codigo = await Montar(ConfiguracionValida, viriato, handler).RunRobotAsync(ct: cts.Token);

        Assert.Equal(0, codigo);
        Assert.Contains("completar:hecho:Caso 1", viriato.Llamadas);
    }

    [Fact]
    public async Task ARejectedApiKey_StopsTheRobotWithExitCodeOne()
    {
        var viriato = new FakeViriato { ErrorAlConsultar = new ViriatoApiException(HttpStatusCode.Unauthorized, null) };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var codigo = await Montar(ConfiguracionValida, viriato, new FakeHandler()).RunRobotAsync(ct: cts.Token);

        Assert.Equal(1, codigo);
        Assert.False(cts.IsCancellationRequested, "it must stop by itself, not by the test's timeout");
    }

    [Fact]
    public async Task AThrowingHandler_FailsTheStepAndTheRobotKeepsRunning()
    {
        var viriato = new FakeViriato();
        viriato.Cola.Enqueue(NuevaEjecucion());
        viriato.Cola.Enqueue(NuevaEjecucion());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var intento = 0;
        var handler = new FakeHandler
        {
            Accion = _ =>
            {
                if (++intento == 2) cts.Cancel();
                return intento == 1 ? throw new InvalidOperationException("boom") : Task.FromResult(RpaStepResult.Completado("ok"));
            },
        };

        var codigo = await Montar(ConfiguracionValida, viriato, handler).RunRobotAsync(ct: cts.Token);

        Assert.Equal(0, codigo);
        Assert.Contains("fallar:boom", viriato.Llamadas);
        Assert.Contains("completar:ok", viriato.Llamadas);
    }
}

public sealed class NavegadorOptionsTests
{
    [Fact]
    public void WithoutAConfiguredFolder_DownloadsGoToATempSubfolder()
    {
        var carpeta = new NavegadorOptions().CarpetaDescargasEfectiva();

        Assert.StartsWith(Path.GetTempPath(), carpeta);
    }

    [Fact]
    public void AConfiguredFolder_IsHonoured()
    {
        Assert.Equal(@"D:\descargas", new NavegadorOptions { CarpetaDescargas = @"D:\descargas" }.CarpetaDescargasEfectiva());
    }
}
