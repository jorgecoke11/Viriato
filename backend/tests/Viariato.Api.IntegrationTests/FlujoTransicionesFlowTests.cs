using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Authorization;
using Viriato.Rpa.Client;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>
/// The arrow-shaped flow: a version's steps are its servicios, and arrows (servicio + how its execution ended + the Caso's business
/// state → next servicio) decide where a Caso goes next. The P01 layout of BSH is the running example: the extractor hands a Caso to the
/// store updater only when it leaves it in PRECIOS_EXTRAIDOS.
/// </summary>
public sealed class FlujoTransicionesFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private sealed record Entorno(HttpClient Admin, Guid FlujoId, Dictionary<string, Guid> Servicios, Dictionary<string, string> Claves);

    /// <summary>A process with one Rpa servicio (and a running Despliegue, so a robot can claim its steps) per name.</summary>
    private async Task<Entorno> CrearEntornoAsync(params string[] servicios)
    {
        var admin = factory.CreateClient();
        var email = $"flechas-{Guid.NewGuid():N}@example.com";
        var register = await admin.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "SuperSecret123", displayName = "Admin" });
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

        var flujoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"P01 {Guid.NewGuid():N}", descripcion = (string?)null }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/asignaciones", new { userId })).StatusCode);
        var equipoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/equipos", new { nombre = $"Equipo {Guid.NewGuid():N}", descripcion = (string?)null }));

        var ids = new Dictionary<string, Guid>();
        var claves = new Dictionary<string, string>();
        foreach (var nombre in servicios)
        {
            ids[nombre] = await IdAsync(await admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"{nombre} {Guid.NewGuid():N}", descripcion = (string?)null }));
            var despliegue = await admin.PostAsJsonAsync("/api/v1/despliegues", new { equipoId, servicioId = ids[nombre], flujoId });
            Assert.Equal(HttpStatusCode.OK, despliegue.StatusCode);
            claves[nombre] = (await despliegue.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("apiKey").GetString()!;
        }

        return new Entorno(admin, flujoId, ids, claves);
    }

    private static object Paso(Entorno e, string servicio, int orden, int? maximoDeVisitas = null) => new
    {
        clave = servicio, orden, nombre = servicio, tipoPaso = "Rpa", agenteDefinicionId = (Guid?)null, servicioId = (Guid?)e.Servicios[servicio],
        configuracionJson = "{\"aplicacion\":\"Portal\"}", posicionX = orden * 280.0, posicionY = 0.0, maximoDeVisitas,
    };

    private static object Flecha(string? origen, string destino, string? resultado = null, string? estado = null) =>
        new { origen, destino, resultado, estadoNegocioCodigo = estado };

    private static async Task<HttpResponseMessage> GuardarAsync(Entorno e, Guid versionId, object[] pasos, object[] transiciones) =>
        await e.Admin.PutAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/versiones/{versionId}/pasos", new { pasos, transiciones });

    private async Task<Guid> PublicarAsync(Entorno e, object[] pasos, object[] transiciones)
    {
        var versionId = await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/versiones", new { notas = (string?)null }));
        var guardado = await GuardarAsync(e, versionId, pasos, transiciones);
        Assert.True(guardado.IsSuccessStatusCode, await guardado.Content.ReadAsStringAsync());
        var publicado = await e.Admin.PostAsync($"/api/v1/flujos/{e.FlujoId}/versiones/{versionId}/publicar", null);
        Assert.True(publicado.IsSuccessStatusCode, await publicado.Content.ReadAsStringAsync());
        return versionId;
    }

    /// <summary>The P01 of BSH: Inicio → Extractor; Extractor · Completada · PRECIOS_EXTRAIDOS → Actualizador.</summary>
    private Task<Guid> PublicarP01Async(Entorno e) => PublicarAsync(e,
        [Paso(e, "Extractor", 1), Paso(e, "Actualizador", 2)],
        [Flecha(null, "Extractor"), Flecha("Extractor", "Actualizador", "Completada", "PRECIOS_EXTRAIDOS")]);

    private async Task<Guid> CrearCasoAsync(Entorno e)
    {
        var respuesta = await e.Admin.PostAsJsonAsync("/api/v1/casos", new { flujoId = e.FlujoId, titulo = "Lavadoras Balay", datosJson = (string?)null });
        return await IdAsync(respuesta);
    }

    private async Task<string> EstadoCasoAsync(Entorno e, Guid casoId) =>
        (await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}")).GetProperty("estado").GetString()!;

    private RpaClient Robot(Entorno e, string servicio)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", e.Claves[servicio]);
        return new RpaClient(http);
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task AVersionWithArrows_IsSavedAndReadBack_WithTheStatesTheArrowsNameCreated_AndNoRpaConfiguration()
    {
        var e = await CrearEntornoAsync("Extractor", "Actualizador");
        var versionId = await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/versiones", new { notas = (string?)null }));

        var guardado = await GuardarAsync(e, versionId,
            [Paso(e, "Extractor", 1), Paso(e, "Actualizador", 2, maximoDeVisitas: 3)],
            [Flecha(null, "Extractor"), Flecha("Extractor", "Actualizador", "Completada", "PRECIOS_EXTRAIDOS")]);
        Assert.Equal(HttpStatusCode.OK, guardado.StatusCode);

        var version = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/versiones/{versionId}");
        var pasos = version.GetProperty("pasos").EnumerateArray().ToList();
        var extractor = pasos.Single(p => p.GetProperty("nombre").GetString() == "Extractor");
        var actualizador = pasos.Single(p => p.GetProperty("nombre").GetString() == "Actualizador");
        Assert.Equal(JsonValueKind.Null, extractor.GetProperty("configuracionJson").ValueKind);
        Assert.Equal(280.0, extractor.GetProperty("posicionX").GetDouble());
        Assert.Equal(3, actualizador.GetProperty("maximoDeVisitas").GetInt32());

        var flechas = version.GetProperty("transiciones").EnumerateArray().ToList();
        Assert.Equal(2, flechas.Count);
        Assert.Equal(JsonValueKind.Null, flechas[0].GetProperty("pasoOrigenId").ValueKind);
        Assert.Equal(extractor.GetProperty("id").GetGuid(), flechas[0].GetProperty("pasoDestinoId").GetGuid());
        Assert.Equal(extractor.GetProperty("id").GetGuid(), flechas[1].GetProperty("pasoOrigenId").GetGuid());
        Assert.Equal("Completada", flechas[1].GetProperty("resultado").GetString());
        Assert.Equal("PRECIOS_EXTRAIDOS", flechas[1].GetProperty("estadoNegocioCodigo").GetString());
        Assert.Equal(actualizador.GetProperty("id").GetGuid(), flechas[1].GetProperty("pasoDestinoId").GetGuid());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var estado = await db.Set<FlujoEstadoDef>().SingleAsync(x => x.FlujoId == e.FlujoId && x.Codigo == "PRECIOS_EXTRAIDOS");
        Assert.Equal("Precios extraidos", estado.Display);
    }

    [Fact]
    public async Task InvalidGraphs_AreRejected()
    {
        var e = await CrearEntornoAsync("Extractor", "Actualizador");
        var versionId = await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/versiones", new { notas = (string?)null }));

        // No start arrow.
        Assert.Equal(HttpStatusCode.BadRequest, (await GuardarAsync(e, versionId,
            [Paso(e, "Extractor", 1)], [])).StatusCode);

        // The same servicio twice.
        var repetido = new
        {
            clave = "Otra", orden = 2, nombre = "Otra", tipoPaso = "Rpa", agenteDefinicionId = (Guid?)null,
            servicioId = (Guid?)e.Servicios["Extractor"], configuracionJson = (string?)null,
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await GuardarAsync(e, versionId,
            [Paso(e, "Extractor", 1), repetido], [Flecha(null, "Extractor")])).StatusCode);

        // Two arrows for the same servicio, result and state.
        Assert.Equal(HttpStatusCode.BadRequest, (await GuardarAsync(e, versionId,
            [Paso(e, "Extractor", 1), Paso(e, "Actualizador", 2)],
            [Flecha(null, "Extractor"), Flecha("Extractor", "Actualizador", "Completada"), Flecha("Extractor", "Extractor", "Completada")])).StatusCode);

        // An arrow to a step that is not there, or without a result.
        Assert.Equal(HttpStatusCode.BadRequest, (await GuardarAsync(e, versionId,
            [Paso(e, "Extractor", 1)], [Flecha(null, "Extractor"), Flecha("Extractor", "Nadie", "Completada")])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await GuardarAsync(e, versionId,
            [Paso(e, "Extractor", 1)], [Flecha(null, "Extractor"), Flecha("Extractor", "Extractor")])).StatusCode);
    }

    [Fact]
    public async Task AStepNoArrowReaches_BlocksPublishing()
    {
        var e = await CrearEntornoAsync("Extractor", "Actualizador");
        var versionId = await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/versiones", new { notas = (string?)null }));
        Assert.Equal(HttpStatusCode.OK, (await GuardarAsync(e, versionId,
            [Paso(e, "Extractor", 1), Paso(e, "Actualizador", 2)], [Flecha(null, "Extractor")])).StatusCode);

        var publicado = await e.Admin.PostAsync($"/api/v1/flujos/{e.FlujoId}/versiones/{versionId}/publicar", null);

        Assert.Equal(HttpStatusCode.Conflict, publicado.StatusCode);
        Assert.Contains("Actualizador", await publicado.Content.ReadAsStringAsync());
    }

    private static object Creador(string nombre, string? pasoInicial) => new
    {
        nombre, descripcion = (string?)null, tipoCasoId = (Guid?)null, pasoInicialNombre = pasoInicial,
        estadoNegocioInicialId = (Guid?)null, plantillaTitulo = (string?)null, orden = 1, activo = true,
    };

    [Fact]
    public async Task AStepNoArrowReaches_CanBePublished_WhenACreatorStartsThere_AndCasosItOpensEndThere()
    {
        // The real P01: a creating flow on its own (Crear casos, no arrows) and the pricing flow (Extractor → Actualizador).
        var e = await CrearEntornoAsync("Creador", "Extractor", "Actualizador");
        var versionId = await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/versiones", new { notas = (string?)null }));
        Assert.Equal(HttpStatusCode.OK, (await GuardarAsync(e, versionId,
            [Paso(e, "Extractor", 1), Paso(e, "Actualizador", 2), Paso(e, "Creador", 3)],
            [Flecha(null, "Extractor"), Flecha("Extractor", "Actualizador", "Completada", "PRECIOS_EXTRAIDOS")])).StatusCode);

        // Without a creator starting at it, «Creador» is a dead step.
        Assert.Equal(HttpStatusCode.Conflict, (await e.Admin.PostAsync($"/api/v1/flujos/{e.FlujoId}/versiones/{versionId}/publicar", null)).StatusCode);

        await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/creadores", Creador("Crear casos masivamente", "Creador")));
        var publicado = await e.Admin.PostAsync($"/api/v1/flujos/{e.FlujoId}/versiones/{versionId}/publicar", null);
        Assert.True(publicado.IsSuccessStatusCode, await publicado.Content.ReadAsStringAsync());

        // A Caso started where that creator starts runs the creating step and, with no arrow leaving it, ends there.
        var version = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/versiones/{versionId}");
        var pasoCreador = version.GetProperty("pasos").EnumerateArray().Single(p => p.GetProperty("nombre").GetString() == "Creador").GetProperty("id").GetGuid();
        var casoId = await IdAsync(await e.Admin.PostAsJsonAsync("/api/v1/casos", new { flujoId = e.FlujoId, titulo = "Lanzamiento", pasoInicialId = pasoCreador }));
        var paso = await Robot(e, "Creador").ObtenerSiguienteEjecucionAsync();
        Assert.NotNull(paso);
        Assert.Equal(casoId, paso.CasoId);
        Assert.Null(await Robot(e, "Extractor").ObtenerSiguienteEjecucionAsync());
        await Robot(e, "Creador").CompletarPasoAsync(paso.EjecucionPasoId);
        Assert.Equal("Completado", await EstadoCasoAsync(e, casoId));
        Assert.Null(await Robot(e, "Extractor").ObtenerSiguienteEjecucionAsync());
    }

    [Fact]
    public async Task AVersionWithoutTheStepAnActiveCreatorStartsAt_CannotBePublished()
    {
        var e = await CrearEntornoAsync("Extractor", "Actualizador");
        await PublicarP01Async(e);
        await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/creadores", Creador("Crear Balay", "Extractor")));

        // The next version names its steps after something else: the creator would be left without its step.
        var versionId = await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/versiones", new { notas = (string?)null }));
        var renombrado = new
        {
            clave = "Extractor", orden = 1, nombre = "BSH - Extractor de precios", tipoPaso = "Rpa", agenteDefinicionId = (Guid?)null,
            servicioId = (Guid?)e.Servicios["Extractor"], configuracionJson = (string?)null,
        };
        Assert.Equal(HttpStatusCode.OK, (await GuardarAsync(e, versionId, [renombrado], [Flecha(null, "Extractor")])).StatusCode);

        var publicado = await e.Admin.PostAsync($"/api/v1/flujos/{e.FlujoId}/versiones/{versionId}/publicar", null);

        Assert.Equal(HttpStatusCode.Conflict, publicado.StatusCode);
        var detalle = await publicado.Content.ReadAsStringAsync();
        Assert.Contains("Crear Balay", detalle);
        Assert.Contains("Extractor", detalle);
    }

    [Fact]
    public async Task AnExtractionLeftInPreciosExtraidos_GoesOnToTheUpdater()
    {
        var e = await CrearEntornoAsync("Extractor", "Actualizador");
        await PublicarP01Async(e);
        var casoId = await CrearCasoAsync(e);

        var extractor = Robot(e, "Extractor");
        var paso = await extractor.ObtenerSiguienteEjecucionAsync();
        Assert.NotNull(paso);
        Assert.StartsWith("Extractor", paso.AplicacionObjetivo); // no configuration: the servicio's name
        await extractor.CambiarEstadoCasoAsync(paso.EjecucionPasoId, "PRECIOS_EXTRAIDOS");
        await extractor.CompletarPasoAsync(paso.EjecucionPasoId);

        var siguiente = await Robot(e, "Actualizador").ObtenerSiguienteEjecucionAsync();
        Assert.NotNull(siguiente);
        Assert.Equal(casoId, siguiente.CasoId);
        await Robot(e, "Actualizador").CompletarPasoAsync(siguiente.EjecucionPasoId);

        // No arrow leaves the updater: the Caso ends completed.
        Assert.Equal("Completado", await EstadoCasoAsync(e, casoId));
    }

    [Fact]
    public async Task AnExtractionLeftInAnotherState_EndsTheCaso_WithoutReachingTheUpdater()
    {
        var e = await CrearEntornoAsync("Extractor", "Actualizador");
        await PublicarP01Async(e);
        var casoId = await CrearCasoAsync(e);

        var extractor = Robot(e, "Extractor");
        var paso = await extractor.ObtenerSiguienteEjecucionAsync();
        await extractor.CambiarEstadoCasoAsync(paso!.EjecucionPasoId, "PRECIOS_EXTRAIDOS_SIN_ACTUALIZAR");
        await extractor.CompletarPasoAsync(paso.EjecucionPasoId);

        Assert.Equal("Completado", await EstadoCasoAsync(e, casoId));
        Assert.Null(await Robot(e, "Actualizador").ObtenerSiguienteEjecucionAsync());
    }

    [Fact]
    public async Task AFailedExecution_FollowsItsFailureArrow_OrFailsTheCasoWithoutOne()
    {
        var e = await CrearEntornoAsync("Extractor", "Revision", "Actualizador");
        await PublicarAsync(e,
            [Paso(e, "Extractor", 1), Paso(e, "Revision", 2), Paso(e, "Actualizador", 3)],
            [
                Flecha(null, "Extractor"),
                Flecha("Extractor", "Revision", "Fallida"),
                Flecha("Extractor", "Actualizador", "Completada"),
            ]);

        var casoId = await CrearCasoAsync(e);
        var paso = await Robot(e, "Extractor").ObtenerSiguienteEjecucionAsync();
        await Robot(e, "Extractor").FallarPasoAsync(paso!.EjecucionPasoId, "el portal no responde");

        var revision = await Robot(e, "Revision").ObtenerSiguienteEjecucionAsync();
        Assert.NotNull(revision);
        Assert.Equal(casoId, revision.CasoId);
        Assert.Equal("EnProgreso", await EstadoCasoAsync(e, casoId));

        // The review step has no failure arrow: failing it fails the Caso.
        await Robot(e, "Revision").FallarPasoAsync(revision.EjecucionPasoId, "no se puede arreglar");
        Assert.Equal("Fallido", await EstadoCasoAsync(e, casoId));
        Assert.Null(await Robot(e, "Actualizador").ObtenerSiguienteEjecucionAsync());
    }

    [Fact]
    public async Task AnArrowForTheExactState_WinsOverOneForAnyState()
    {
        var e = await CrearEntornoAsync("Extractor", "Actualizador", "Avisos");
        await PublicarAsync(e,
            [Paso(e, "Extractor", 1), Paso(e, "Actualizador", 2), Paso(e, "Avisos", 3)],
            [
                Flecha(null, "Extractor"),
                Flecha("Extractor", "Avisos", "Completada"),
                Flecha("Extractor", "Actualizador", "Completada", "PRECIOS_EXTRAIDOS"),
            ]);

        // In the named state: the exact arrow.
        var primero = await CrearCasoAsync(e);
        var paso = await Robot(e, "Extractor").ObtenerSiguienteEjecucionAsync();
        await Robot(e, "Extractor").CambiarEstadoCasoAsync(paso!.EjecucionPasoId, "PRECIOS_EXTRAIDOS");
        await Robot(e, "Extractor").CompletarPasoAsync(paso.EjecucionPasoId);
        Assert.Equal(primero, (await Robot(e, "Actualizador").ObtenerSiguienteEjecucionAsync())?.CasoId);

        // In any other state: the arrow for any state.
        var segundo = await CrearCasoAsync(e);
        paso = await Robot(e, "Extractor").ObtenerSiguienteEjecucionAsync();
        await Robot(e, "Extractor").CompletarPasoAsync(paso!.EjecucionPasoId);
        Assert.Equal(segundo, (await Robot(e, "Avisos").ObtenerSiguienteEjecucionAsync())?.CasoId);
    }

    [Fact]
    public async Task ALoopBackToTheSameServicio_StopsAtItsMaximumOfVisits()
    {
        var e = await CrearEntornoAsync("Extractor");
        await PublicarAsync(e,
            [Paso(e, "Extractor", 1, maximoDeVisitas: 2)],
            [Flecha(null, "Extractor"), Flecha("Extractor", "Extractor", "Fallida")]);
        var casoId = await CrearCasoAsync(e);
        var robot = Robot(e, "Extractor");

        var primero = await robot.ObtenerSiguienteEjecucionAsync();
        await robot.FallarPasoAsync(primero!.EjecucionPasoId, "uno");
        var segundo = await robot.ObtenerSiguienteEjecucionAsync();
        Assert.NotNull(segundo);
        Assert.Equal(casoId, segundo.CasoId);
        await robot.FallarPasoAsync(segundo.EjecucionPasoId, "dos");

        Assert.Equal("Fallido", await EstadoCasoAsync(e, casoId));
        Assert.Null(await robot.ObtenerSiguienteEjecucionAsync());
    }

    [Fact]
    public async Task ACasoAlreadyClosedInAState_DoesNotMove_WhenAVersionWithAnArrowFromThatStateIsPublished()
    {
        var e = await CrearEntornoAsync("Extractor", "Actualizador");
        // The older, list-shaped version: just the extractor.
        var antigua = await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/versiones", new { notas = (string?)null }));
        Assert.Equal(HttpStatusCode.OK, (await e.Admin.PutAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/versiones/{antigua}/pasos", new
        {
            pasos = new[] { new { orden = 1, nombre = "Extractor", tipoPaso = "Rpa", agenteDefinicionId = (Guid?)null, servicioId = (Guid?)e.Servicios["Extractor"], configuracionJson = (string?)null } },
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await e.Admin.PostAsync($"/api/v1/flujos/{e.FlujoId}/versiones/{antigua}/publicar", null)).StatusCode);

        var casoId = await CrearCasoAsync(e);
        var paso = await Robot(e, "Extractor").ObtenerSiguienteEjecucionAsync();
        await Robot(e, "Extractor").CambiarEstadoCasoAsync(paso!.EjecucionPasoId, "PRECIOS_EXTRAIDOS");
        await Robot(e, "Extractor").CompletarPasoAsync(paso.EjecucionPasoId);
        Assert.Equal("Completado", await EstadoCasoAsync(e, casoId));

        await PublicarP01Async(e);

        Assert.Null(await Robot(e, "Actualizador").ObtenerSiguienteEjecucionAsync());
        Assert.Equal("Completado", await EstadoCasoAsync(e, casoId));
    }
}
