using System.Net;
using System.Text;
using System.Text.Json;
using Viariato.ApiContracts;
using Viriato.Rpa.Client;

namespace Viriato.Rpa.Client.Tests;

public sealed class RpaClientTests
{
    private sealed record CapturedRequest(HttpMethod Method, Uri Uri, string? ApiKey, string Body, string? ContentType);

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!,
                request.Headers.TryGetValues("X-Api-Key", out var keys) ? keys.Single() : null,
                body,
                request.Content?.Headers.ContentType?.MediaType));
            return respond(request);
        }
    }

    private static (RpaClient Client, FakeHandler Handler) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage>? respond = null, string baseUrl = "http://viriato.test")
    {
        var handler = new FakeHandler(respond ?? (_ => new HttpResponseMessage(HttpStatusCode.NoContent)));
        var http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        http.DefaultRequestHeaders.Add("X-Api-Key", "clave-de-prueba");
        return (new RpaClient(http), handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task ObtenerEstadoAsync_CallsTheDespliegueRouteWithTheApiKeyAndMapsTheResponse()
    {
        var (client, handler) = CreateClient(_ => Json(HttpStatusCode.OK,
            """{"encendido":true,"equipoNombre":"PC-01","servicioNombre":"Robot Alta","flujoNombre":"Proceso Alta"}"""));

        var estado = await client.ObtenerEstadoAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("http://viriato.test/api/v1/rpa/despliegue", request.Uri.ToString());
        Assert.Equal("clave-de-prueba", request.ApiKey);
        Assert.Equal(new DespliegueEstadoDto(true, "PC-01", "Robot Alta", "Proceso Alta"), estado);
    }

    [Fact]
    public async Task ObtenerSiguienteEjecucionAsync_ReturnsNullWhenTheQueueIsEmpty()
    {
        var (client, handler) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        Assert.Null(await client.ObtenerSiguienteEjecucionAsync());

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v1/rpa/cola/siguiente", request.Uri.AbsolutePath);
    }

    [Fact]
    public async Task ObtenerSiguienteEjecucionAsync_MapsTheClaimedStep()
    {
        var pasoId = Guid.NewGuid();
        var casoId = Guid.NewGuid();
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK,
            $$"""{"ejecucionPasoId":"{{pasoId}}","casoId":"{{casoId}}","casoTitulo":"Caso 1","flujoNombre":"Proceso Alta","aplicacionObjetivo":"Portal","parametrosEntrada":"{\"a\":1}","datosCasoJson":"{\"proveedor\":\"Balay\"}"}"""));

        var ejecucion = await client.ObtenerSiguienteEjecucionAsync();

        Assert.Equal(new EjecucionAsignadaDto(pasoId, casoId, "Caso 1", "Proceso Alta", "Portal", "{\"a\":1}", "{\"proveedor\":\"Balay\"}"), ejecucion);
    }

    [Theory]
    [InlineData("completar", "parametrosSalida", "{\"ok\":true}")]
    [InlineData("completar-caso", "parametrosSalida", "{\"ok\":true}")]
    [InlineData("fallar", "error", "se cayó el portal")]
    [InlineData("estado-negocio", "codigo", "PENDIENTE_DOCS")]
    public async Task StepWrites_PostTheExpectedJsonToTheExpectedRoute(string route, string field, string value)
    {
        var pasoId = Guid.NewGuid();
        var (client, handler) = CreateClient();

        await (route switch
        {
            "completar" => client.CompletarPasoAsync(pasoId, value),
            "completar-caso" => client.CompletarCasoAsync(pasoId, value),
            "fallar" => client.FallarPasoAsync(pasoId, value),
            _ => client.CambiarEstadoCasoAsync(pasoId, value),
        });

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"/api/v1/rpa/pasos/{pasoId}/{route}", request.Uri.AbsolutePath);
        Assert.Equal("application/json", request.ContentType);
        Assert.Equal(value, JsonDocument.Parse(request.Body).RootElement.GetProperty(field).GetString());
    }

    [Fact]
    public async Task ReportarEnVivoAsync_PostsOnlyWhatWasGiven_ToTheLiveRoute()
    {
        var pasoId = Guid.NewGuid();
        var (client, handler) = CreateClient();

        await client.ReportarEnVivoAsync(pasoId, porcentaje: 40, mensaje: "Añadiendo productos", vistaUrl: "http://localhost:6080/vnc.html");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"/api/v1/rpa/pasos/{pasoId}/en-vivo", request.Uri.AbsolutePath);
        var cuerpo = JsonDocument.Parse(request.Body).RootElement;
        Assert.Equal(40, cuerpo.GetProperty("porcentaje").GetInt32());
        Assert.Equal("Añadiendo productos", cuerpo.GetProperty("mensaje").GetString());
        Assert.Equal("http://localhost:6080/vnc.html", cuerpo.GetProperty("vistaUrl").GetString());
    }

    [Fact]
    public async Task AgregarEvidenciaAsync_SendsAMultipartFormWithTheFileAndFields()
    {
        var pasoId = Guid.NewGuid();
        var (client, handler) = CreateClient();

        await client.AgregarEvidenciaAsync(
            pasoId, EvidenciaTipo.Screenshot, "Portal de altas", contenidoJson: "{\"x\":1}",
            archivo: new MemoryStream("contenido-png"u8.ToArray()), nombreArchivo: "captura.png");

        var request = Assert.Single(handler.Requests);
        Assert.Equal($"/api/v1/rpa/pasos/{pasoId}/evidencias", request.Uri.AbsolutePath);
        Assert.Equal("multipart/form-data", request.ContentType);
        Assert.Contains("name=tipo", request.Body);
        Assert.Contains("Screenshot", request.Body);
        Assert.Contains("Portal de altas", request.Body);
        Assert.Contains("name=contenidoJson", request.Body);
        Assert.Contains("name=file; filename=captura.png", request.Body);
        Assert.Contains("contenido-png", request.Body);
    }

    [Fact]
    public async Task AgregarEvidenciaAsync_WithoutAFileOrJson_OmitsThoseParts()
    {
        var (client, handler) = CreateClient();

        await client.AgregarEvidenciaAsync(Guid.NewGuid(), EvidenciaTipo.Otro, "Solo título");

        var body = Assert.Single(handler.Requests).Body;
        Assert.DoesNotContain("name=file", body);
        Assert.DoesNotContain("name=contenidoJson", body);
    }

    [Fact]
    public async Task ObtenerParametrosAsync_ReadsTheProcessSettingsAsACaseInsensitiveMap()
    {
        var (client, handler) = CreateClient(_ => Json(HttpStatusCode.OK, """{"iva":"21","n_maximo_carrito":"14","cupon":""}"""));

        var parametros = await client.ObtenerParametrosAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v1/rpa/parametros", request.Uri.AbsolutePath);
        Assert.Equal("clave-de-prueba", request.ApiKey);
        Assert.Equal("21", parametros.Texto("IVA"));
        Assert.Equal(14, parametros.EnteroObligatorio("n_maximo_carrito"));
        Assert.Equal("", parametros.Texto("cupon"));
    }

    [Fact]
    public async Task ObtenerParametrosAsync_AProcessWithoutSettings_GivesAnEmptySet()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.OK, "{}"));

        var parametros = await client.ObtenerParametrosAsync();

        Assert.Empty(parametros.Valores);
        Assert.Null(parametros.Texto("iva"));
    }

    [Fact]
    public async Task ObtenerCredencialAsync_AsksForTheNamedCredentialAndMapsTheAnswer()
    {
        var (client, handler) = CreateClient(_ => Json(HttpStatusCode.OK, """{"nombre":"tradeplace","usuario":"robot","password":"s3cr3to"}"""));

        var credencial = await client.ObtenerCredencialAsync("tradeplace");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/api/v1/rpa/credenciales/tradeplace", request.Uri.AbsolutePath);
        Assert.Equal("clave-de-prueba", request.ApiKey);
        Assert.Equal(new CredencialRobotDto("tradeplace", "robot", "s3cr3to"), credencial);
    }

    [Fact]
    public async Task ObtenerCredencialAsync_EscapesTheNameInTheUrl()
    {
        var (client, handler) = CreateClient(_ => Json(HttpStatusCode.OK, """{"nombre":"a/b","usuario":null,"password":"x"}"""));

        await client.ObtenerCredencialAsync("a/b ?x");

        Assert.Equal("/api/v1/rpa/credenciales/a%2Fb%20%3Fx", Assert.Single(handler.Requests).Uri.AbsolutePath);
    }

    [Fact]
    public async Task ObtenerCredencialAsync_AMissingOrForbiddenCredential_IsANotFoundException()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.NotFound, """{"title":"Not found","status":404,"detail":"Credencial no encontrada."}"""));

        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => client.ObtenerCredencialAsync("no-existe"));

        Assert.True(ex.IsNotFound);
        Assert.Equal("Credencial no encontrada.", ex.Detail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ObtenerCredencialAsync_RejectsAnEmptyName_WithoutCallingTheApi(string nombre)
    {
        var (client, handler) = CreateClient();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => client.ObtenerCredencialAsync(nombre));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CrearCasoAsync_PostsTheRequestAndMapsTheCreatedCaso()
    {
        var id = Guid.NewGuid();
        var (client, handler) = CreateClient(_ => Json(HttpStatusCode.OK, $$"""{"casoId":"{{id}}","titulo":"Lavadoras Bosch"}"""));

        var creado = await client.CrearCasoAsync(new CrearCasoRobotRequest("Lavadoras Bosch", """{"beneficio":15}""", "Bosch", "EN_COLA", "Extraer precios"));

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v1/rpa/casos", request.Uri.AbsolutePath);
        Assert.Equal("clave-de-prueba", request.ApiKey);
        using var body = JsonDocument.Parse(request.Body);
        Assert.Equal("Lavadoras Bosch", body.RootElement.GetProperty("titulo").GetString());
        Assert.Equal("""{"beneficio":15}""", body.RootElement.GetProperty("datosJson").GetString());
        Assert.Equal("Bosch", body.RootElement.GetProperty("tipoCaso").GetString());
        Assert.Equal("EN_COLA", body.RootElement.GetProperty("estadoNegocioCodigo").GetString());
        Assert.Equal("Extraer precios", body.RootElement.GetProperty("pasoInicial").GetString());
        Assert.Equal(new CasoCreadoDto(id, "Lavadoras Bosch"), creado);
    }

    [Fact]
    public async Task CrearCasoAsync_ADespliegueWithoutPermission_IsAForbiddenException()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.Forbidden, """{"status":403,"detail":"Este despliegue no tiene permiso para crear casos."}"""));

        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => client.CrearCasoAsync(new CrearCasoRobotRequest("x", null)));

        Assert.True(ex.IsForbidden);
        Assert.Contains("permiso", ex.Detail);
    }

    [Fact]
    public async Task CrearCasoAsync_RejectsANullRequest_WithoutCallingTheApi()
    {
        var (client, handler) = CreateClient();

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.CrearCasoAsync(null!));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AnErrorStatus_SurfacesAsViriatoApiExceptionWithTheServersDetail()
    {
        var (client, _) = CreateClient(_ => Json(HttpStatusCode.Conflict,
            """{"type":"https://viariato.app/errors/conflict","title":"Conflict","status":409,"detail":"Este paso ya no está en progreso."}"""));

        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => client.CompletarPasoAsync(Guid.NewGuid()));

        Assert.True(ex.IsConflict);
        Assert.False(ex.IsTransient);
        Assert.Equal("Este paso ya no está en progreso.", ex.Detail);
        Assert.Contains("409", ex.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, true, false)]
    [InlineData(HttpStatusCode.NotFound, false, false)]
    [InlineData(HttpStatusCode.InternalServerError, false, true)]
    [InlineData(HttpStatusCode.TooManyRequests, false, true)]
    public async Task ErrorStatusFlags_ClassifyTheFailure(HttpStatusCode status, bool unauthorized, bool transient)
    {
        var (client, _) = CreateClient(_ => new HttpResponseMessage(status));

        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => client.ObtenerEstadoAsync());

        Assert.Equal(unauthorized, ex.IsUnauthorized);
        Assert.Equal(transient, ex.IsTransient);
        Assert.Null(ex.Detail);
    }

    [Fact]
    public async Task AnErrorPageThatIsNotJson_StillThrowsWithJustTheStatus()
    {
        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>Bad gateway</html>", Encoding.UTF8, "text/html"),
        });

        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => client.ObtenerEstadoAsync());

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Null(ex.Detail);
    }

    [Fact]
    public async Task StandaloneConstructor_AppliesTheApiKeyAndKeepsABaseUrlPathPrefix()
    {
        using var client = new RpaClient(new RpaClientOptions { BaseUrl = "https://empresa.test/viriato/", ApiKey = "mi-clave" });
        var field = typeof(RpaClient).GetField("_http", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var http = (HttpClient)field.GetValue(client)!;

        Assert.Equal("mi-clave", http.DefaultRequestHeaders.GetValues("X-Api-Key").Single());
        Assert.Equal("https://empresa.test/viriato/api/v1/rpa/despliegue", new Uri(http.BaseAddress!, "api/v1/rpa/despliegue").ToString());
        await Task.CompletedTask;
    }

    [Theory]
    [InlineData("", "clave")]
    [InlineData("http://viriato.test", "")]
    [InlineData("no-es-una-url", "clave")]
    [InlineData("ftp://viriato.test", "clave")]
    public void StandaloneConstructor_RejectsAMissingOrInvalidConfiguration(string baseUrl, string apiKey)
    {
        Assert.Throws<ArgumentException>(() => new RpaClient(new RpaClientOptions { BaseUrl = baseUrl, ApiKey = apiKey }));
    }
}
