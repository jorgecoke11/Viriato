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
using Viriato.Rpa.Lanzador;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>
/// What a launcher — the process that starts a robot only while it has work — asks Viriato and tells it, through the
/// launcher's own client, against the real Api and a real Postgres.
/// </summary>
public sealed class LanzadorFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private sealed record Escenario(HttpClient Admin, Guid EquipoId, Guid FlujoId, Guid DespliegueId, string ApiKey);

    private static async Task<Guid> IdOfAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Escenario> CreateEscenarioAsync()
    {
        var client = factory.CreateClient();
        var email = $"lanzador-robots-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "SuperSecret123", displayName = "Admin" });
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

        var versionId = await IdOfAsync(await client.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        var pasos = await client.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new object[]
            {
                new { orden = 1, nombre = "Robot", tipoPaso = "Rpa", agenteDefinicionId = (Guid?)null, servicioId = (Guid?)servicioId, configuracionJson = "{\"aplicacion\":\"Portal\"}" },
            },
        });
        Assert.Equal(HttpStatusCode.OK, pasos.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null)).StatusCode);

        var despliegue = await client.PostAsJsonAsync("/api/v1/despliegues", new { equipoId, servicioId, flujoId });
        Assert.Equal(HttpStatusCode.OK, despliegue.StatusCode);
        var cuerpo = await despliegue.Content.ReadFromJsonAsync<JsonElement>();

        return new Escenario(client, equipoId, flujoId, cuerpo.GetProperty("despliegue").GetProperty("id").GetGuid(), cuerpo.GetProperty("apiKey").GetString()!);
    }

    private static Task StartCasoAsync(Escenario e, string titulo) =>
        e.Admin.PostAsJsonAsync("/api/v1/casos", new { flujoId = e.FlujoId, flujoVersionId = (Guid?)null, titulo, datosJson = (string?)null });

    private ViriatoDelLanzador Lanzador() => new(factory.CreateClient());

    private RpaClient Robot(string apiKey)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return new RpaClient(http);
    }

    private static async Task<JsonElement> RobotEnLaColaAsync(Escenario e)
    {
        var cola = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/equipos/{e.EquipoId}/despacho/cola");
        return cola.GetProperty("robots").EnumerateArray().Single(r => r.GetProperty("despliegueId").GetGuid() == e.DespliegueId);
    }

    [Fact]
    public async Task TheLauncher_IsToldWhatWaitsAndWhatRuns_AsTheRobotTakesIt()
    {
        var e = await CreateEscenarioAsync();
        var lanzador = Lanzador();

        Assert.Equal(new TrabajoDelDespliegueDto(true, 0, 0, false), await lanzador.TrabajoAsync(e.ApiKey, default));

        await StartCasoAsync(e, "Uno");
        await StartCasoAsync(e, "Dos");
        Assert.Equal(new TrabajoDelDespliegueDto(true, 2, 0, false), await lanzador.TrabajoAsync(e.ApiKey, default));

        var robot = Robot(e.ApiKey);
        var ejecucion = await robot.ObtenerSiguienteEjecucionAsync();
        Assert.Equal(new TrabajoDelDespliegueDto(true, 1, 1, false), await lanzador.TrabajoAsync(e.ApiKey, default));

        await robot.CompletarPasoAsync(ejecucion!.EjecucionPasoId);
        Assert.Equal(new TrabajoDelDespliegueDto(true, 1, 0, false), await lanzador.TrabajoAsync(e.ApiKey, default));
    }

    [Fact]
    public async Task ASwitchedOffDespliegue_IsReportedAsOff_WithItsQueueIntact()
    {
        var e = await CreateEscenarioAsync();
        await StartCasoAsync(e, "Espera");
        Assert.Equal(HttpStatusCode.OK, (await e.Admin.PatchAsJsonAsync($"/api/v1/despliegues/{e.DespliegueId}", new { encendido = false })).StatusCode);

        Assert.Equal(new TrabajoDelDespliegueDto(false, 1, 0, false), await Lanzador().TrabajoAsync(e.ApiKey, default));
    }

    [Fact]
    public async Task TheLauncherAsking_DoesNotMakeTheRobotLookConnected_ButTheRobotItselfDoes()
    {
        var e = await CreateEscenarioAsync();
        var lanzador = Lanzador();

        // Asked again and again, as the launcher does: the robot is still not there, so the dispatcher does not wait for it.
        for (var i = 0; i < 3; i++) await lanzador.TrabajoAsync(e.ApiKey, default);
        await lanzador.ReportarAsync(e.ApiKey, new EstadoDelLanzadorRequest(EstadosDelLanzador.SinTrabajo, 0, 1), default);

        var antes = await RobotEnLaColaAsync(e);
        Assert.False(antes.GetProperty("conectado").GetBoolean());
        Assert.Equal(0, antes.GetProperty("instancias").GetInt32());
        var despliegue = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/despliegues/{e.DespliegueId}");
        Assert.Equal(JsonValueKind.Null, despliegue.GetProperty("lastUsedAt").ValueKind);

        // The robot itself calling is another matter.
        await Robot(e.ApiKey).ObtenerSiguienteEjecucionAsync();

        var despues = await RobotEnLaColaAsync(e);
        Assert.True(despues.GetProperty("conectado").GetBoolean());
    }

    [Fact]
    public async Task WhatTheLauncherReports_IsShownOnTheMachinesQueue()
    {
        var e = await CreateEscenarioAsync();
        Assert.Equal(JsonValueKind.Null, (await RobotEnLaColaAsync(e)).GetProperty("lanzador").ValueKind);

        await Lanzador().ReportarAsync(
            e.ApiKey, new EstadoDelLanzadorRequest(EstadosDelLanzador.EnEspera, 0, 2, "Esperando hueco: el lanzador ya tiene 2 de 2 robots en marcha."), default);

        var lanzador = (await RobotEnLaColaAsync(e)).GetProperty("lanzador");
        Assert.Equal("EnEspera", lanzador.GetProperty("estado").GetString());
        Assert.Equal(0, lanzador.GetProperty("copias").GetInt32());
        Assert.Equal(2, lanzador.GetProperty("maxCopias").GetInt32());
        Assert.StartsWith("Esperando hueco", lanzador.GetProperty("detalle").GetString());

        // Only the latest word counts.
        await Lanzador().ReportarAsync(e.ApiKey, new EstadoDelLanzadorRequest(EstadosDelLanzador.EnMarcha, 1, 2), default);
        lanzador = (await RobotEnLaColaAsync(e)).GetProperty("lanzador");
        Assert.Equal("EnMarcha", lanzador.GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.Null, lanzador.GetProperty("detalle").ValueKind);
    }

    [Fact]
    public async Task ALauncherThatHasGoneQuiet_IsNoLongerShown()
    {
        var e = await CreateEscenarioAsync();
        await Lanzador().ReportarAsync(e.ApiKey, new EstadoDelLanzadorRequest(EstadosDelLanzador.EnMarcha, 1, 1), default);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hace = DateTimeOffset.UtcNow.AddMinutes(-10);
            await db.Set<Viariato.Modules.RpaFleet.Domain.Despliegue>().Where(d => d.Id == e.DespliegueId)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.LanzadorVistoEn, hace));
        }

        Assert.Equal(JsonValueKind.Null, (await RobotEnLaColaAsync(e)).GetProperty("lanzador").ValueKind);
    }

    [Fact]
    public async Task AnUnknownState_IsRejected_AndAWrongKeyIsRefused()
    {
        var e = await CreateEscenarioAsync();
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", e.ApiKey);

        var respuesta = await http.PutAsJsonAsync("/api/v1/rpa/lanzador/estado", new EstadoDelLanzadorRequest("Inventado", 0, 1));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        await Assert.ThrowsAsync<ClaveRechazadaException>(() => Lanzador().TrabajoAsync("vrt_no_existe", default));
    }
}
