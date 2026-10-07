using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.ApiContracts;
using Viariato.Infrastructure;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Authorization;
using Viriato.Rpa.Client;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>
/// The Viriato.Rpa.Client library, exercised the way a real robot would: through its own API key, over
/// HTTP, against the real Api and a real Postgres — not against a fake. The unit tests in
/// Viriato.Rpa.Client.Tests pin the request shapes; this proves the server actually accepts them.
/// </summary>
public sealed class RpaClientFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private const string DatosDelCaso = "{\"proveedor\":\"Balay\",\"beneficio\":15}";

    private sealed record Escenario(HttpClient Admin, Guid FlujoId, Guid ServicioId, string ApiKey);

    private static async Task<Guid> IdOfAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>A published Flujo with an Rpa step (backed by a Servicio) followed by an Interno one, a
    /// business state to switch to, and a switched-on Despliegue — returning the API key a robot would be given.</summary>
    private async Task<Escenario> CreateEscenarioAsync()
    {
        var client = factory.CreateClient();
        var email = $"rpa-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "SuperSecret123", displayName = "Admin RPA" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("id").GetGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var adminRole = await db.Set<Role>().SingleAsync(r => r.Name == SystemRoles.Admin);
            db.Add(new UserRole { UserId = userId, RoleId = adminRole.Id, GrantedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "SuperSecret123" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var equipoId = await IdOfAsync(await client.PostAsJsonAsync("/api/v1/equipos", new { nombre = $"Equipo {Guid.NewGuid():N}", descripcion = (string?)null }));
        var servicioId = await IdOfAsync(await client.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Servicio {Guid.NewGuid():N}", descripcion = (string?)null }));
        var flujoId = await IdOfAsync(await client.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Flujo {Guid.NewGuid():N}", descripcion = (string?)null }));

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/asignaciones", new { userId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            $"/api/v1/flujos/{flujoId}/estados", new { codigo = "EN_REVISION", display = "En revisión", orden = 1, esFinal = false })).StatusCode);

        var versionId = await IdOfAsync(await client.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        var pasos = await client.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new object[]
            {
                new { orden = 1, nombre = "Robot", tipoPaso = "Rpa", agenteDefinicionId = (Guid?)null, servicioId = (Guid?)servicioId, configuracionJson = "{\"aplicacion\":\"Portal\"}" },
                new { orden = 2, nombre = "Cierre", tipoPaso = "Interno", agenteDefinicionId = (Guid?)null, servicioId = (Guid?)null, configuracionJson = (string?)null },
            },
        });
        Assert.Equal(HttpStatusCode.OK, pasos.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null)).StatusCode);

        var despliegue = await client.PostAsJsonAsync("/api/v1/despliegues", new { equipoId, servicioId, flujoId });
        Assert.Equal(HttpStatusCode.OK, despliegue.StatusCode);
        var apiKey = (await despliegue.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("apiKey").GetString()!;

        return new Escenario(client, flujoId, servicioId, apiKey);
    }

    private static async Task<Guid> StartCasoAsync(Escenario escenario, string titulo) =>
        await IdOfAsync(await escenario.Admin.PostAsJsonAsync(
            "/api/v1/casos", new { flujoId = escenario.FlujoId, flujoVersionId = (Guid?)null, titulo, datosJson = DatosDelCaso }));

    private static async Task<string> CasoEstadoAsync(Escenario escenario, Guid casoId) =>
        (await escenario.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}")).GetProperty("estado").GetString()!;

    private RpaClient RobotClient(string apiKey)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return new RpaClient(http);
    }

    [Fact]
    public async Task ARobot_ClaimsReportsAndClosesTheCaso_UsingOnlyTheLibrary()
    {
        var escenario = await CreateEscenarioAsync();
        var robot = RobotClient(escenario.ApiKey);

        var estado = await robot.ObtenerEstadoAsync();
        Assert.True(estado.Encendido);
        Assert.True(await robot.IsActivoAsync());
        Assert.Null(await robot.ObtenerSiguienteEjecucionAsync());

        var casoId = await StartCasoAsync(escenario, "Caso del robot");

        var ejecucion = await robot.ObtenerSiguienteEjecucionAsync();
        Assert.NotNull(ejecucion);
        Assert.Equal(casoId, ejecucion.CasoId);
        Assert.Equal("Portal", ejecucion.AplicacionObjetivo);
        // jsonb comes back normalised (keys reordered, spaces added), so compare content, not text.
        var datos = JsonDocument.Parse(ejecucion.DatosCasoJson!).RootElement;
        Assert.Equal("Balay", datos.GetProperty("proveedor").GetString());
        Assert.Equal(15, datos.GetProperty("beneficio").GetInt32());
        Assert.Null(await robot.ObtenerSiguienteEjecucionAsync());

        await robot.AgregarEvidenciaAsync(
            ejecucion.EjecucionPasoId, EvidenciaTipo.Screenshot, "Pantalla de confirmación",
            archivo: new MemoryStream("png"u8.ToArray()), nombreArchivo: "pantalla.png");
        await robot.CambiarEstadoCasoAsync(ejecucion.EjecucionPasoId, "EN_REVISION");

        // The step is not the last of the Flujo, yet CompletarCaso must close the whole Caso right away.
        await robot.CompletarCasoAsync(ejecucion.EjecucionPasoId, "{\"ok\":true}");

        Assert.Equal("Completado", await CasoEstadoAsync(escenario, casoId));
        var caso = await escenario.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}");
        Assert.Equal("EN_REVISION", caso.GetProperty("estadoNegocio").GetProperty("codigo").GetString());
        var timeline = await escenario.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}/timeline");
        Assert.Contains(timeline.EnumerateArray(), item => item.GetProperty("titulo").GetString() == "Pantalla de confirmación");
    }

    [Fact]
    public async Task AFailedStep_IsReportedThroughTheLibraryAndFailsTheCaso()
    {
        var escenario = await CreateEscenarioAsync();
        var robot = RobotClient(escenario.ApiKey);
        var casoId = await StartCasoAsync(escenario, "Caso que falla");

        var ejecucion = await robot.ObtenerSiguienteEjecucionAsync();
        await robot.FallarPasoAsync(ejecucion!.EjecucionPasoId, "el portal no responde");

        Assert.Equal("Fallido", await CasoEstadoAsync(escenario, casoId));
    }

    [Fact]
    public async Task TheWorker_ProcessesTheQueueAndCompletesTheStepsInOrder()
    {
        var escenario = await CreateEscenarioAsync();
        var casoId = await StartCasoAsync(escenario, "Caso del worker");
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var worker = new RpaWorker(RobotClient(escenario.ApiKey), new RpaWorkerOptions { IntervaloPolling = TimeSpan.FromMilliseconds(50) });

        string? visto = null;
        await worker.RunAsync(async (paso, ct) =>
        {
            visto = paso.Ejecucion.CasoTitulo;
            await paso.CambiarEstadoCasoAsync("EN_REVISION", ct);
            cts.Cancel(); // one step is enough; the report still goes out after the worker is told to stop
            return RpaStepResult.Completado();
        }, cts.Token);

        Assert.Equal("Caso del worker", visto);
        // A plain Completado lets the Caso run on to its Interno step, which closes it by itself.
        Assert.Equal("Completado", await CasoEstadoAsync(escenario, casoId));
    }

    [Fact]
    public async Task AWrongApiKey_IsRejectedAsUnauthorized()
    {
        var robot = RobotClient("rpa_clave-que-no-existe");

        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => robot.ObtenerEstadoAsync());

        Assert.True(ex.IsUnauthorized);
    }

    [Fact]
    public async Task ASwitchedOffDespliegue_CannotClaim_AndTheLibrarySurfacesTheConflict()
    {
        var escenario = await CreateEscenarioAsync();
        var robot = RobotClient(escenario.ApiKey);
        var despliegues = await escenario.Admin.GetFromJsonAsync<JsonElement>("/api/v1/despliegues");
        var despliegueId = despliegues.GetProperty("items").EnumerateArray()
            .First(d => d.GetProperty("flujoId").GetGuid() == escenario.FlujoId).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await escenario.Admin.PatchAsJsonAsync($"/api/v1/despliegues/{despliegueId}", new { encendido = false })).StatusCode);

        Assert.False(await robot.IsActivoAsync());
        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => robot.ObtenerSiguienteEjecucionAsync());

        Assert.True(ex.IsConflict);
    }
}
