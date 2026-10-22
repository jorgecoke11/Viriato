using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.ApiContracts;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Authorization;
using Viriato.Rpa.Client;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>
/// The form of a Caso's data belongs to the process and is chosen by its creators. Whoever creates or edits a Caso — a person through a
/// creator or by hand, a launcher, a robot naming a creator — gets the same verdict the form gives; the Caso remembers its form for
/// later edits. A Caso without a form (no creator, or a creator without one) keeps taking any JSON object.
/// </summary>
public sealed class FormulariosFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private const string Esquema = """
        {
          "type": "object",
          "required": ["beneficio"],
          "properties": {
            "beneficio": { "type": "number", "title": "Beneficio", "minimum": 0, "maximum": 100 },
            "actualizar": { "type": "boolean", "title": "Actualizar precios" },
            "marca": { "type": "string", "title": "Marca" }
          }
        }
        """;

    private sealed record Entorno(HttpClient Admin, Guid FlujoId, Guid FormularioId, Guid CreadorId, string KeyLanzador);

    private async Task<Entorno> CrearEntornoAsync()
    {
        var admin = factory.CreateClient();
        var email = $"formulario-{Guid.NewGuid():N}@example.com";

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
        var servicioId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Servicio {Guid.NewGuid():N}", descripcion = (string?)null }));
        var flujoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Proceso {Guid.NewGuid():N}", descripcion = (string?)null }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/asignaciones", new { userId })).StatusCode);

        var versionId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        var pasos = await admin.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new object[]
            {
                new { orden = 1, nombre = "Lanzar", tipoPaso = "Rpa", agenteDefinicionId = (Guid?)null, servicioId = (Guid?)servicioId, configuracionJson = "{\"aplicacion\":\"Viriato\"}" },
                new { orden = 2, nombre = "Cierre", tipoPaso = "Interno", agenteDefinicionId = (Guid?)null, servicioId = (Guid?)null, configuracionJson = (string?)null },
            },
        });
        Assert.Equal(HttpStatusCode.OK, pasos.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null)).StatusCode);

        var formularioId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/formularios", new { nombre = "Precios", esquemaJson = Esquema }));
        var creadorId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/creadores", CuerpoCreador("Revisar precios", formularioId, """{"marca":"Balay"}""")));

        var despliegue = await admin.PostAsJsonAsync("/api/v1/despliegues", new { equipoId, servicioId, flujoId, flujoDestinoId = flujoId });
        Assert.Equal(HttpStatusCode.OK, despliegue.StatusCode);
        var key = (await despliegue.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("apiKey").GetString()!;

        return new Entorno(admin, flujoId, formularioId, creadorId, key);
    }

    private static object CuerpoCreador(string nombre, Guid? formularioId, string? defectos) => new
    {
        nombre, descripcion = (string?)null, tipoCasoId = (Guid?)null, pasoInicialNombre = "Cierre", estadoNegocioInicialId = (Guid?)null,
        plantillaTitulo = "{datos.marca} #{n}", orden = 1, activo = true, formularioId, datosPorDefectoJson = defectos,
    };

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> IniciarCaso(Entorno e, Guid? formulario, string? datosJson, string titulo = "Caso") =>
        e.Admin.PostAsJsonAsync("/api/v1/casos", new { flujoId = e.FlujoId, titulo, datosJson, formularioId = formulario });

    private static Task<HttpResponseMessage> CrearDesdeCreador(Entorno e, string? datosJson) =>
        e.Admin.PostAsJsonAsync($"/api/v1/creadores-de-caso/{e.CreadorId}/casos", new { titulo = (string?)null, datosJson });

    private static async Task<string> MensajesAsync(HttpResponseMessage response) =>
        string.Join(" ", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateArray().Select(m => m.GetString())));

    private RpaClient Robot(string apiKey)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return new RpaClient(http);
    }

    private async Task<Caso> CasoAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Caso>().AsNoTracking().SingleAsync(c => c.Id == id);
    }

    // ---------------------------------------------------------------- the library of forms

    [Fact]
    public async Task TheSchemaIsStoredExactlyAsWritten_AndTheListSaysWhichCreatorsUseIt()
    {
        var e = await CrearEntornoAsync();

        var formularios = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/formularios");
        var precios = formularios.EnumerateArray().Single();

        Assert.Equal(Esquema, precios.GetProperty("esquemaJson").GetString());
        Assert.Equal(["Revisar precios"], precios.GetProperty("creadores").EnumerateArray().Select(c => c.GetString()).ToList());
    }

    [Theory]
    [InlineData("{no es json", "JSON válido")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"uuid"}}}""", "no está soportado")]
    [InlineData("""{"type":"object","properties":{}}""", "ningún campo")]
    public async Task ABadSchema_IsRejected_OnCreateAndOnReplace_WithTheReasons(string esquema, string fragmento)
    {
        var e = await CrearEntornoAsync();

        var crear = await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/formularios", new { nombre = "Roto", esquemaJson = esquema });
        Assert.Equal(HttpStatusCode.BadRequest, crear.StatusCode);
        Assert.Contains(fragmento, await MensajesAsync(crear));

        var reemplazar = await e.Admin.PutAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/formularios/{e.FormularioId}", new { nombre = "Precios", esquemaJson = esquema });
        Assert.Equal(HttpStatusCode.BadRequest, reemplazar.StatusCode);
        Assert.Contains(fragmento, await MensajesAsync(reemplazar));
    }

    [Fact]
    public async Task AFormInUse_CannotBeDeleted_AndOneOnlyCasosRemember_Can_LeavingThemFreeForm()
    {
        var e = await CrearEntornoAsync();
        var caso = await IdAsync(await IniciarCaso(e, e.FormularioId, """{"beneficio":10}"""));

        var enUso = await e.Admin.DeleteAsync($"/api/v1/flujos/{e.FlujoId}/formularios/{e.FormularioId}");
        Assert.Equal(HttpStatusCode.Conflict, enUso.StatusCode);
        Assert.Contains("Revisar precios", await enUso.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, (await e.Admin.PutAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/creadores/{e.CreadorId}", CuerpoCreador("Revisar precios", null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.DeleteAsync($"/api/v1/flujos/{e.FlujoId}/formularios/{e.FormularioId}")).StatusCode);

        Assert.Null((await CasoAsync(caso)).FormularioId);
        Assert.Equal(HttpStatusCode.OK, (await e.Admin.PatchAsJsonAsync($"/api/v1/casos/{caso}/datos", new { datosJson = """{"beneficio":"ya no se comprueba"}""" })).StatusCode);
    }

    [Fact]
    public async Task AFormOfAnotherProcess_CannotBeUsed()
    {
        var e = await CrearEntornoAsync();
        var otro = await CrearEntornoAsync();

        Assert.Equal(HttpStatusCode.Conflict, (await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/creadores", CuerpoCreador("Ajeno", otro.FormularioId, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await IniciarCaso(e, otro.FormularioId, """{"beneficio":10}""")).StatusCode);
    }

    // ---------------------------------------------------------------- the creator's defaults

    [Fact]
    public async Task TheDefaults_MayLeaveRequiredFieldsOut_ButWhatTheyHaveMustFit()
    {
        var e = await CrearEntornoAsync();

        var fuera = await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/creadores", CuerpoCreador("Fuera de rango", e.FormularioId, """{"beneficio":500}"""));
        Assert.Equal(HttpStatusCode.Conflict, fuera.StatusCode);
        Assert.Contains("como máximo 100", await fuera.Content.ReadAsStringAsync());

        var lista = await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/creadores", CuerpoCreador("Lista", e.FormularioId, """[{"beneficio":5}]"""));
        Assert.Equal(HttpStatusCode.BadRequest, lista.StatusCode);
    }

    [Fact]
    public async Task ChangingAForm_SoThatACreatorsDefaultsNoLongerFit_IsRefused()
    {
        var e = await CrearEntornoAsync();
        const string marcaNumerica = """{"type":"object","properties":{"marca":{"type":"number"}}}""";

        var respuesta = await e.Admin.PutAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/formularios/{e.FormularioId}", new { nombre = "Precios", esquemaJson = marcaNumerica });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("Revisar precios", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheListOfUsableCreators_CarriesTheFormAndItsDefaults()
    {
        var e = await CrearEntornoAsync();

        var lista = await e.Admin.GetFromJsonAsync<JsonElement>("/api/v1/creadores-de-caso");
        var suyo = lista.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == e.CreadorId);

        Assert.Equal(Esquema, suyo.GetProperty("esquemaDatosJson").GetString());
        Assert.Contains("Balay", suyo.GetProperty("datosPorDefectoJson").GetString());
    }

    // ---------------------------------------------------------------- creating and editing Casos

    [Fact]
    public async Task APerson_ThroughACreator_GetsTheFormsVerdict_AndTheDefaultsAreNotForcedOnThem()
    {
        var e = await CrearEntornoAsync();

        var sinDatos = await CrearDesdeCreador(e, null);
        Assert.Equal(HttpStatusCode.BadRequest, sinDatos.StatusCode);
        Assert.Contains("Beneficio: es obligatorio", await MensajesAsync(sinDatos));

        // The person emptied the brand the form came with: it stays empty.
        var bien = await CrearDesdeCreador(e, """{"beneficio":15}""");
        Assert.Equal(HttpStatusCode.OK, bien.StatusCode);
        var caso = await CasoAsync((await bien.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        Assert.DoesNotContain("Balay", caso.DatosJson);
        Assert.Equal(e.FormularioId, caso.FormularioId);
    }

    [Fact]
    public async Task StartingACasoByHand_WithAForm_RequiresDataThatFitsIt_AndARejectedOneIsNotCreated()
    {
        var e = await CrearEntornoAsync();
        var titulo = $"Rechazado {Guid.NewGuid():N}";

        var fueraDeRango = await IniciarCaso(e, e.FormularioId, """{"beneficio":500}""", titulo);
        Assert.Equal(HttpStatusCode.BadRequest, fueraDeRango.StatusCode);
        Assert.Contains("como máximo 100", await MensajesAsync(fueraDeRango));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Set<Caso>().AnyAsync(c => c.Titulo == titulo));

        Assert.Equal(HttpStatusCode.OK, (await IniciarCaso(e, e.FormularioId, """{"beneficio":15,"actualizar":true}""")).StatusCode);
    }

    [Fact]
    public async Task CasosWithoutAForm_KeepTakingAnyJsonObject()
    {
        var e = await CrearEntornoAsync();

        Assert.Equal(HttpStatusCode.OK, (await IniciarCaso(e, null, """{"lo-que":"sea"}""")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await IniciarCaso(e, null, null)).StatusCode);
    }

    [Fact]
    public async Task EditingTheData_IsCheckedAgainstTheFormTheCasoWasBornWith()
    {
        var e = await CrearEntornoAsync();
        var casoId = await IdAsync(await CrearDesdeCreador(e, """{"beneficio":15}"""));

        var mal = await e.Admin.PatchAsJsonAsync($"/api/v1/casos/{casoId}/datos", new { datosJson = """{"beneficio":"mucho"}""" });
        Assert.Equal(HttpStatusCode.BadRequest, mal.StatusCode);
        Assert.Contains("Beneficio: debe ser un número", await MensajesAsync(mal));

        Assert.Equal(HttpStatusCode.OK, (await e.Admin.PatchAsJsonAsync($"/api/v1/casos/{casoId}/datos", new { datosJson = """{"beneficio":20}""" })).StatusCode);
    }

    // ---------------------------------------------------------------- robots

    [Fact]
    public async Task ARobotNamingACreator_GetsItsDefaults_ItsForm_AndItsTitle()
    {
        var e = await CrearEntornoAsync();
        var robot = Robot(e.KeyLanzador);

        var mal = await Assert.ThrowsAsync<ViriatoApiException>(() =>
            robot.CrearCasoAsync(new CrearCasoRobotRequest(null, """{"actualizar":true}""", Creador: "revisar PRECIOS")));
        Assert.Equal(HttpStatusCode.BadRequest, mal.StatusCode);

        var bien = await robot.CrearCasoAsync(new CrearCasoRobotRequest(null, """{"beneficio":15}""", Creador: "Revisar precios"));

        Assert.Equal("Balay #1", bien.Titulo);
        var caso = await CasoAsync(bien.CasoId);
        Assert.Contains("Balay", caso.DatosJson);
        Assert.Equal(e.FormularioId, caso.FormularioId);
    }

    [Fact]
    public async Task ARobotWithoutACreator_WritesFreeFormData_AndMustGiveATitle()
    {
        var e = await CrearEntornoAsync();
        var robot = Robot(e.KeyLanzador);

        var libre = await robot.CrearCasoAsync(new CrearCasoRobotRequest("Libre", """{"beneficio":"lo que sea"}""", PasoInicial: "Cierre"));
        Assert.Null((await CasoAsync(libre.CasoId)).FormularioId);

        var sinTitulo = await Assert.ThrowsAsync<ViriatoApiException>(() => robot.CrearCasoAsync(new CrearCasoRobotRequest(null, null, PasoInicial: "Cierre")));
        Assert.Equal(HttpStatusCode.BadRequest, sinTitulo.StatusCode);

        var noExiste = await Assert.ThrowsAsync<ViriatoApiException>(() => robot.CrearCasoAsync(new CrearCasoRobotRequest("X", null, Creador: "No existe")));
        Assert.Equal(HttpStatusCode.Conflict, noExiste.StatusCode);
    }
}
