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
/// A case type can describe its business data as a form (a JSON schema). Whoever creates or edits a Caso of that
/// type — a person through the API, or a launcher robot — gets the same verdict the form gives; a type without a
/// schema, or a Caso without a type, keeps taking any JSON object.
/// </summary>
public sealed class EsquemaDatosFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private const string Esquema = """
        {
          "type": "object",
          "required": ["beneficio"],
          "properties": {
            "beneficio": { "type": "number", "title": "Beneficio", "minimum": 0, "maximum": 100 },
            "actualizar": { "type": "boolean", "title": "Actualizar precios" }
          }
        }
        """;

    private sealed record Entorno(HttpClient Admin, Guid FlujoId, Guid TipoConEsquema, Guid TipoLibre, string KeyLanzador);

    private async Task<Entorno> CrearEntornoAsync()
    {
        var admin = factory.CreateClient();
        var email = $"esquema-{Guid.NewGuid():N}@example.com";

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

        var conEsquema = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/tipos-caso", new { nombre = "Con formulario", orden = 1, esquemaDatosJson = Esquema }));
        var libre = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/tipos-caso", new { nombre = "Libre", orden = 2 }));

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

        var despliegue = await admin.PostAsJsonAsync("/api/v1/despliegues", new { equipoId, servicioId, flujoId, flujoDestinoId = flujoId });
        Assert.Equal(HttpStatusCode.OK, despliegue.StatusCode);
        var key = (await despliegue.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("apiKey").GetString()!;

        return new Entorno(admin, flujoId, conEsquema, libre, key);
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> IniciarCaso(Entorno e, Guid? tipo, string? datosJson, string titulo = "Caso") =>
        e.Admin.PostAsJsonAsync("/api/v1/casos", new { flujoId = e.FlujoId, titulo, datosJson, tipoCasoId = tipo });

    private static async Task<string> MensajesAsync(HttpResponseMessage response) =>
        string.Join(" ", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateArray().Select(m => m.GetString())));

    private RpaClient Robot(string apiKey)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return new RpaClient(http);
    }

    // ---------------------------------------------------------------- defining the form

    [Fact]
    public async Task TheSchemaIsStoredExactlyAsWritten_SoTheFieldOrderSurvives()
    {
        var e = await CrearEntornoAsync();

        var tipos = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/tipos-caso");
        var conFormulario = tipos.EnumerateArray().Single(t => t.GetProperty("id").GetGuid() == e.TipoConEsquema);

        Assert.Equal(Esquema, conFormulario.GetProperty("esquemaDatosJson").GetString());
        var libre = tipos.EnumerateArray().Single(t => t.GetProperty("id").GetGuid() == e.TipoLibre);
        Assert.Equal(JsonValueKind.Null, libre.GetProperty("esquemaDatosJson").ValueKind);
    }

    [Theory]
    [InlineData("{no es json", "JSON válido")]
    [InlineData("""{"type":"object","properties":{"a":{"type":"uuid"}}}""", "no está soportado")]
    [InlineData("""{"type":"object","properties":{}}""", "ningún campo")]
    public async Task ABadSchema_IsRejected_OnCreateAndOnUpdate_WithTheReasons(string esquema, string fragmento)
    {
        var e = await CrearEntornoAsync();

        var crear = await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/tipos-caso", new { nombre = "Roto", orden = 3, esquemaDatosJson = esquema });
        Assert.Equal(HttpStatusCode.BadRequest, crear.StatusCode);
        Assert.Contains(fragmento, await MensajesAsync(crear));

        var actualizar = await e.Admin.PatchAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/tipos-caso/{e.TipoLibre}", new { esquemaDatosJson = esquema });
        Assert.Equal(HttpStatusCode.BadRequest, actualizar.StatusCode);
        Assert.Contains(fragmento, await MensajesAsync(actualizar));
    }

    [Fact]
    public async Task AnAdmin_CanAddAndRemoveTheForm_OfAnExistingType()
    {
        var e = await CrearEntornoAsync();

        var puesto = await e.Admin.PatchAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/tipos-caso/{e.TipoLibre}", new { esquemaDatosJson = Esquema });
        Assert.Equal(HttpStatusCode.OK, puesto.StatusCode);
        Assert.Equal(Esquema, (await puesto.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("esquemaDatosJson").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await IniciarCaso(e, e.TipoLibre, "{}")).StatusCode);

        var quitado = await e.Admin.PatchAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/tipos-caso/{e.TipoLibre}", new { quitarEsquema = true });
        Assert.Equal(JsonValueKind.Null, (await quitado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("esquemaDatosJson").ValueKind);
        Assert.Equal(HttpStatusCode.OK, (await IniciarCaso(e, e.TipoLibre, "{}")).StatusCode);
    }

    [Fact]
    public async Task APatchThatOnlyRenames_LeavesTheFormAlone()
    {
        var e = await CrearEntornoAsync();

        var renombrado = await e.Admin.PatchAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/tipos-caso/{e.TipoConEsquema}", new { nombre = "Otro nombre" });

        Assert.Equal(Esquema, (await renombrado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("esquemaDatosJson").GetString());
    }

    // ---------------------------------------------------------------- creating and editing Casos

    [Fact]
    public async Task StartingACaso_OfATypeWithAForm_RequiresDataThatFitsIt()
    {
        var e = await CrearEntornoAsync();

        var sinDatos = await IniciarCaso(e, e.TipoConEsquema, null);
        Assert.Equal(HttpStatusCode.BadRequest, sinDatos.StatusCode);
        Assert.Contains("Beneficio: es obligatorio", await MensajesAsync(sinDatos));

        var fueraDeRango = await IniciarCaso(e, e.TipoConEsquema, """{"beneficio":500}""");
        Assert.Equal(HttpStatusCode.BadRequest, fueraDeRango.StatusCode);
        Assert.Contains("como máximo 100", await MensajesAsync(fueraDeRango));

        var bien = await IniciarCaso(e, e.TipoConEsquema, """{"beneficio":15,"actualizar":true}""");
        Assert.Equal(HttpStatusCode.OK, bien.StatusCode);
    }

    [Fact]
    public async Task ARejectedCaso_IsNotCreatedAtAll()
    {
        var e = await CrearEntornoAsync();
        var titulo = $"Rechazado {Guid.NewGuid():N}";

        Assert.Equal(HttpStatusCode.BadRequest, (await IniciarCaso(e, e.TipoConEsquema, "{}", titulo)).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cuantos = await db.Database.SqlQuery<int>($"select count(*)::int as \"Value\" from casos.casos where titulo = {titulo}").SingleAsync();
        Assert.Equal(0, cuantos);
    }

    [Fact]
    public async Task FreeFormTypesAndUntypedCasos_KeepTakingAnyJsonObject()
    {
        var e = await CrearEntornoAsync();

        Assert.Equal(HttpStatusCode.OK, (await IniciarCaso(e, e.TipoLibre, """{"lo-que":"sea"}""")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await IniciarCaso(e, null, """{"lo-que":"sea"}""")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await IniciarCaso(e, null, null)).StatusCode);
    }

    [Fact]
    public async Task EditingTheData_OfACasoOfATypeWithAForm_IsCheckedToo()
    {
        var e = await CrearEntornoAsync();
        var caso = await IniciarCaso(e, e.TipoConEsquema, """{"beneficio":15}""");
        var casoId = (await caso.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var mal = await e.Admin.PatchAsJsonAsync($"/api/v1/casos/{casoId}/datos", new { datosJson = """{"beneficio":"mucho"}""" });
        Assert.Equal(HttpStatusCode.BadRequest, mal.StatusCode);
        Assert.Contains("Beneficio: debe ser un número", await MensajesAsync(mal));

        var bien = await e.Admin.PatchAsJsonAsync($"/api/v1/casos/{casoId}/datos", new { datosJson = """{"beneficio":20}""" });
        Assert.Equal(HttpStatusCode.OK, bien.StatusCode);
    }

    [Fact]
    public async Task ALauncherRobot_CreatingACasoOfATypeWithAForm_GetsTheSameVerdict()
    {
        var e = await CrearEntornoAsync();
        var robot = Robot(e.KeyLanzador);

        var mal = await Assert.ThrowsAsync<ViriatoApiException>(() =>
            robot.CrearCasoAsync(new CrearCasoRobotRequest("Sin beneficio", """{"actualizar":true}""", "Con formulario", PasoInicial: "Cierre")));
        Assert.Equal(HttpStatusCode.BadRequest, mal.StatusCode);

        var bien = await robot.CrearCasoAsync(new CrearCasoRobotRequest("Con beneficio", """{"beneficio":15}""", "Con formulario", PasoInicial: "Cierre"));
        Assert.Equal("Con beneficio", bien.Titulo);
    }
}
