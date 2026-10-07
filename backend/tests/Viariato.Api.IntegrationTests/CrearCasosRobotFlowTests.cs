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
/// A "launcher" robot opening Cases: only in the one process an admin authorised its Despliegue for, only
/// while switched on, and the Cases it opens are real ones that the destination process's own robot then
/// picks up off its queue.
/// </summary>
public sealed class CrearCasosRobotFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private sealed record Entorno(
        HttpClient Admin, Guid DestinoId, Guid LanzadorId, Guid DespliegueLanzadorId, string KeyLanzador, string KeyDestino);

    /// <param name="mismoProceso">The launcher is step 1 of the very process it creates Casos in (the real BSH layout: step 1
    /// creates the Casos, step 2 is the robot that works them) instead of a process of its own.</param>
    private async Task<Entorno> CrearEntornoAsync(bool autorizarDestino = true, bool publicarDestino = true, bool mismoProceso = false)
    {
        var admin = factory.CreateClient();
        var email = $"lanzador-{Guid.NewGuid():N}@example.com";

        var register = await admin.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "SuperSecret123", displayName = "Admin" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("id").GetGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rol = await db.Set<Role>().SingleAsync(r => r.Name == SystemRoles.Admin);
            db.Add(new UserRole { UserId = userId, RoleId = rol.Id, GrantedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var login = await admin.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "SuperSecret123" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var equipoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/equipos", new { nombre = $"Equipo {Guid.NewGuid():N}", descripcion = (string?)null }));
        var servicioLanzador = await IdAsync(await admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Lanzador {Guid.NewGuid():N}", descripcion = (string?)null }));
        var servicioDestino = await IdAsync(await admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Destino {Guid.NewGuid():N}", descripcion = (string?)null }));
        var destinoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Destino {Guid.NewGuid():N}", descripcion = (string?)null }));
        var lanzadorId = mismoProceso
            ? destinoId
            : await IdAsync(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Lanzador {Guid.NewGuid():N}", descripcion = (string?)null }));

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/flujos/{destinoId}/asignaciones", new { userId })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/flujos/{destinoId}/tipos-caso", new { nombre = "Balay", orden = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(
            $"/api/v1/flujos/{destinoId}/estados", new { codigo = "EN_COLA", display = "En cola", orden = 1, esFinal = false })).StatusCode);

        if (publicarDestino)
        {
            var versionId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{destinoId}/versiones", new { notas = (string?)null }));
            var pasos = await admin.PutAsJsonAsync($"/api/v1/flujos/{destinoId}/versiones/{versionId}/pasos", new
            {
                pasos = mismoProceso
                    ? new object[]
                    {
                        new { orden = 1, nombre = "Lanzar", tipoPaso = "Rpa", agenteDefinicionId = (Guid?)null, servicioId = (Guid?)servicioLanzador, configuracionJson = "{\"aplicacion\":\"Viriato\"}" },
                        new { orden = 2, nombre = "Robot", tipoPaso = "Rpa", agenteDefinicionId = (Guid?)null, servicioId = (Guid?)servicioDestino, configuracionJson = "{\"aplicacion\":\"Portal\"}" },
                    }
                    : new object[]
                    {
                        new { orden = 1, nombre = "Robot", tipoPaso = "Rpa", agenteDefinicionId = (Guid?)null, servicioId = (Guid?)servicioDestino, configuracionJson = "{\"aplicacion\":\"Portal\"}" },
                    },
            });
            Assert.Equal(HttpStatusCode.OK, pasos.StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/flujos/{destinoId}/versiones/{versionId}/publicar", null)).StatusCode);
        }

        var lanzador = await admin.PostAsJsonAsync("/api/v1/despliegues", new
        {
            equipoId, servicioId = servicioLanzador, flujoId = lanzadorId, flujoDestinoId = autorizarDestino ? (Guid?)destinoId : null,
        });
        Assert.Equal(HttpStatusCode.OK, lanzador.StatusCode);
        var cuerpoLanzador = await lanzador.Content.ReadFromJsonAsync<JsonElement>();

        var destino = await admin.PostAsJsonAsync("/api/v1/despliegues", new { equipoId, servicioId = servicioDestino, flujoId = destinoId });
        Assert.Equal(HttpStatusCode.OK, destino.StatusCode);

        return new Entorno(
            admin, destinoId, lanzadorId,
            cuerpoLanzador.GetProperty("despliegue").GetProperty("id").GetGuid(),
            cuerpoLanzador.GetProperty("apiKey").GetString()!,
            (await destino.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("apiKey").GetString()!);
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private RpaClient Robot(string apiKey)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return new RpaClient(http);
    }

    [Fact]
    public async Task ALauncher_OpensACaso_InItsAuthorisedProcess_ThatTheDestinationRobotThenClaims()
    {
        var e = await CrearEntornoAsync();

        var creado = await Robot(e.KeyLanzador).CrearCasoAsync(
            new CrearCasoRobotRequest("Lavadoras Balay", """{"beneficio":15,"producto":"Lavadoras"}""", "balay", "EN_COLA"));
        Assert.Equal("Lavadoras Balay", creado.Titulo);

        var caso = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{creado.CasoId}");
        Assert.Equal(e.DestinoId, caso.GetProperty("flujoId").GetGuid());
        Assert.Equal("Balay", caso.GetProperty("tipoCaso").GetString());
        Assert.Equal("EN_COLA", caso.GetProperty("estadoNegocio").GetProperty("codigo").GetString());
        Assert.Equal(15, JsonDocument.Parse(caso.GetProperty("datosJson").GetString()!).RootElement.GetProperty("beneficio").GetInt32());

        var asignada = await Robot(e.KeyDestino).ObtenerSiguienteEjecucionAsync();
        Assert.NotNull(asignada);
        Assert.Equal(creado.CasoId, asignada.CasoId);
        Assert.Equal("Lavadoras Balay", asignada.CasoTitulo);
    }

    [Fact]
    public async Task ALauncher_CannotOpenCasos_InAnyOtherProcess_ItsKeyOnlyEverReachesTheAuthorisedOne()
    {
        var e = await CrearEntornoAsync();

        // The request has no process field at all, so the only place a Caso can land is the authorised process.
        var creado = await Robot(e.KeyLanzador).CrearCasoAsync(new CrearCasoRobotRequest("Uno", null));

        var caso = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{creado.CasoId}");
        Assert.Equal(e.DestinoId, caso.GetProperty("flujoId").GetGuid());
        Assert.NotEqual(e.LanzadorId, caso.GetProperty("flujoId").GetGuid());
    }

    [Fact]
    public async Task ADespliegueWithoutPermission_IsForbidden_AndNothingIsCreated()
    {
        var e = await CrearEntornoAsync(autorizarDestino: false);

        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => Robot(e.KeyLanzador).CrearCasoAsync(new CrearCasoRobotRequest("No", null)));

        Assert.True(ex.IsForbidden);
        Assert.Null(await Robot(e.KeyDestino).ObtenerSiguienteEjecucionAsync());
    }

    [Fact]
    public async Task APlainWorkerKey_ThatWasNeverGivenThePermission_CannotOpenCasos()
    {
        var e = await CrearEntornoAsync();

        // The destination's own robot is a perfectly valid key, but nobody authorised it to open Casos.
        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => Robot(e.KeyDestino).CrearCasoAsync(new CrearCasoRobotRequest("No", null)));

        Assert.True(ex.IsForbidden);
    }

    [Fact]
    public async Task ASwitchedOffDespliegue_CannotOpenCasos()
    {
        var e = await CrearEntornoAsync();
        Assert.Equal(HttpStatusCode.OK, (await e.Admin.PatchAsJsonAsync($"/api/v1/despliegues/{e.DespliegueLanzadorId}", new { encendido = false })).StatusCode);

        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => Robot(e.KeyLanzador).CrearCasoAsync(new CrearCasoRobotRequest("No", null)));

        Assert.True(ex.IsConflict);
        Assert.Null(await Robot(e.KeyDestino).ObtenerSiguienteEjecucionAsync());
    }

    [Fact]
    public async Task AnUnknownTipoOrEstado_IsAConflict_NamingWhatIsMissing()
    {
        var e = await CrearEntornoAsync();
        var robot = Robot(e.KeyLanzador);

        var tipo = await Assert.ThrowsAsync<ViriatoApiException>(() => robot.CrearCasoAsync(new CrearCasoRobotRequest("x", null, TipoCaso: "Fagor")));
        Assert.True(tipo.IsConflict);
        Assert.Contains("Fagor", tipo.Detail);

        var estado = await Assert.ThrowsAsync<ViriatoApiException>(() => robot.CrearCasoAsync(new CrearCasoRobotRequest("x", null, EstadoNegocioCodigo: "NO_EXISTE")));
        Assert.True(estado.IsConflict);
        Assert.Contains("NO_EXISTE", estado.Detail);

        Assert.Null(await Robot(e.KeyDestino).ObtenerSiguienteEjecucionAsync());
    }

    [Fact]
    public async Task ADestinationWithoutAPublishedVersion_IsAConflict()
    {
        var e = await CrearEntornoAsync(publicarDestino: false);

        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => Robot(e.KeyLanzador).CrearCasoAsync(new CrearCasoRobotRequest("x", null)));

        Assert.True(ex.IsConflict);
        Assert.Contains("versión publicada", ex.Detail);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyTitle_IsRejected(string titulo)
    {
        var e = await CrearEntornoAsync();

        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => Robot(e.KeyLanzador).CrearCasoAsync(new CrearCasoRobotRequest(titulo, null)));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }

    [Theory]
    [InlineData("[1,2]")]
    [InlineData("\"texto\"")]
    [InlineData("{no es json")]
    public async Task DatosThatAreNotAJsonObject_AreRejected(string datos)
    {
        var e = await CrearEntornoAsync();

        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => Robot(e.KeyLanzador).CrearCasoAsync(new CrearCasoRobotRequest("x", datos)));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }

    [Fact]
    public async Task AnAdmin_CanGrantAndRevokeThePermission_AfterTheDespliegueExists()
    {
        var e = await CrearEntornoAsync(autorizarDestino: false);
        var robot = Robot(e.KeyLanzador);
        Assert.True((await Assert.ThrowsAsync<ViriatoApiException>(() => robot.CrearCasoAsync(new CrearCasoRobotRequest("x", null)))).IsForbidden);

        var concedido = await e.Admin.PatchAsJsonAsync($"/api/v1/despliegues/{e.DespliegueLanzadorId}", new { flujoDestinoId = e.DestinoId });
        Assert.Equal(HttpStatusCode.OK, concedido.StatusCode);
        Assert.Equal(e.DestinoId, (await concedido.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("flujoDestinoId").GetGuid());
        await robot.CrearCasoAsync(new CrearCasoRobotRequest("ya sí", null));

        var revocado = await e.Admin.PatchAsJsonAsync($"/api/v1/despliegues/{e.DespliegueLanzadorId}", new { quitarFlujoDestino = true });
        Assert.Equal(JsonValueKind.Null, (await revocado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("flujoDestinoId").ValueKind);
        Assert.True((await Assert.ThrowsAsync<ViriatoApiException>(() => robot.CrearCasoAsync(new CrearCasoRobotRequest("otra vez no", null)))).IsForbidden);
    }

    [Fact]
    public async Task APatchThatOnlyTogglesEncendido_LeavesThePermissionAlone()
    {
        var e = await CrearEntornoAsync();

        var apagado = await e.Admin.PatchAsJsonAsync($"/api/v1/despliegues/{e.DespliegueLanzadorId}", new { encendido = false });

        Assert.Equal(e.DestinoId, (await apagado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("flujoDestinoId").GetGuid());
    }

    [Fact]
    public async Task ALauncherThatIsStep1OfTheProcess_CannotCreateCasosThatWouldStartAtItsOwnStep()
    {
        var e = await CrearEntornoAsync(mismoProceso: true);
        var lanzador = Robot(e.KeyLanzador);

        var sinPaso = await Assert.ThrowsAsync<ViriatoApiException>(() => lanzador.CrearCasoAsync(new CrearCasoRobotRequest("bucle", null)));
        Assert.True(sinPaso.IsConflict);
        Assert.Contains("mismo servicio", sinPaso.Detail);

        var suPropioPaso = await Assert.ThrowsAsync<ViriatoApiException>(() =>
            lanzador.CrearCasoAsync(new CrearCasoRobotRequest("bucle", null, PasoInicial: "lanzar")));
        Assert.True(suPropioPaso.IsConflict);

        Assert.Null(await lanzador.ObtenerSiguienteEjecucionAsync());
        Assert.Null(await Robot(e.KeyDestino).ObtenerSiguienteEjecucionAsync());
    }

    [Fact]
    public async Task ALauncherThatIsStep1_CreatesCasosStartingAtTheNextStep_ThatOnlyTheNextRobotSees()
    {
        var e = await CrearEntornoAsync(mismoProceso: true);
        var lanzador = Robot(e.KeyLanzador);

        // Names match regardless of case, like codes elsewhere.
        var creado = await lanzador.CrearCasoAsync(new CrearCasoRobotRequest("Lavadoras Balay", """{"beneficio":15}""", "Balay", "EN_COLA", PasoInicial: "ROBOT"));

        var caso = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{creado.CasoId}");
        Assert.Equal(e.DestinoId, caso.GetProperty("flujoId").GetGuid());

        // The launcher's own queue stays empty: step 1 of the new Caso was skipped, not queued.
        Assert.Null(await lanzador.ObtenerSiguienteEjecucionAsync());
        var asignada = await Robot(e.KeyDestino).ObtenerSiguienteEjecucionAsync();
        Assert.NotNull(asignada);
        Assert.Equal(creado.CasoId, asignada.CasoId);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var estados = await db.Database
            .SqlQuery<string>($"select estado as \"Value\" from casos.ejecucion_pasos where caso_id = {creado.CasoId} order by created_at")
            .ToListAsync();
        Assert.Equal(["Omitido", "EnProgreso"], estados);

        // The claimed step tells the robot which kind of Caso it is.
        Assert.Equal("Balay", asignada.TipoCaso);
    }

    [Fact]
    public async Task AnUnknownPasoInicial_IsAConflict_NamingIt()
    {
        var e = await CrearEntornoAsync(mismoProceso: true);

        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() =>
            Robot(e.KeyLanzador).CrearCasoAsync(new CrearCasoRobotRequest("x", null, PasoInicial: "Inexistente")));

        Assert.True(ex.IsConflict);
        Assert.Contains("Inexistente", ex.Detail);
    }

    [Fact]
    public async Task TheLaunchersOwnCaso_CanBeClosedWholeWithoutRunningTheNextStep()
    {
        var e = await CrearEntornoAsync(mismoProceso: true);
        var lanzador = Robot(e.KeyLanzador);
        var inicio = await e.Admin.PostAsJsonAsync("/api/v1/casos", new { flujoId = e.DestinoId, titulo = "Lanzamiento", datosJson = """{"beneficio":15}""" });
        Assert.Equal(HttpStatusCode.OK, inicio.StatusCode);
        var casoId = (await inicio.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var paso = await lanzador.ObtenerSiguienteEjecucionAsync();
        Assert.NotNull(paso);
        Assert.Equal(casoId, paso.CasoId);
        await lanzador.CompletarCasoAsync(paso.EjecucionPasoId);

        var caso = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}");
        Assert.Equal("Completado", caso.GetProperty("estado").GetString());
        Assert.Null(await Robot(e.KeyDestino).ObtenerSiguienteEjecucionAsync());
    }
}
