using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Authorization;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>
/// Creators end to end: the author of a process configures them once, and a user creates a case by picking one — the title is
/// written for them unless they choose it, and the user can only use the creators of processes they are assigned to.
/// </summary>
public sealed class CreadoresDeCasoFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private const string EsquemaAlta = """
        { "type": "object", "required": ["cliente"],
          "properties": { "cliente": { "type": "string", "title": "Cliente" } } }
        """;

    private sealed record Entorno(HttpClient Admin, Guid UserId, Guid FlujoId, Guid TipoCasoId, Guid EstadoId, Guid CreadorId);

    private async Task<(HttpClient Client, Guid UserId)> UsuarioAsync(string? rol)
    {
        var client = factory.CreateClient();
        var email = $"creador-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "SuperSecret123", displayName = "Prueba" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("id").GetGuid();

        // Registering gives the User role (which can already use the platform); "no role" here means no permission at all.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RemoveRange(await db.Set<UserRole>().Where(ur => ur.UserId == userId).ToListAsync());
            if (rol is not null)
            {
                var role = await db.Set<Role>().SingleAsync(r => r.Name == rol);
                db.Add(new UserRole { UserId = userId, RoleId = role.Id, GrantedAt = DateTimeOffset.UtcNow });
            }
            await db.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "SuperSecret123" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, userId);
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Entorno> CrearEntornoAsync(string plantilla = "{creador} {fecha} #{n}")
    {
        var (admin, userId) = await UsuarioAsync(SystemRoles.Admin);

        var flujoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Altas {Guid.NewGuid():N}", descripcion = (string?)null }));
        var versionId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new[]
            {
                new { orden = 1, nombre = "Validar", tipoPaso = "Interno", agenteDefinicionId = (Guid?)null, configuracionJson = (string?)null },
                new { orden = 2, nombre = "Dar de alta", tipoPaso = "Interno", agenteDefinicionId = (Guid?)null, configuracionJson = (string?)null },
            },
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/asignaciones", new { userId })).StatusCode);

        var tipoId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/tipos-caso", new { nombre = "Alta", orden = 1, esquemaDatosJson = EsquemaAlta }));
        var estadoId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/estados", new { codigo = "NUEVO", display = "Nuevo", orden = 1, esFinal = false }));

        var creadorId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/creadores", new
        {
            nombre = "Alta de cliente",
            descripcion = "Un cliente nuevo",
            tipoCasoId = tipoId,
            pasoInicialNombre = "Dar de alta",
            estadoNegocioInicialId = estadoId,
            plantillaTitulo = plantilla,
            orden = 1,
        }));

        return new Entorno(admin, userId, flujoId, tipoId, estadoId, creadorId);
    }

    private static async Task<JsonElement> CasoAsync(HttpClient client, Guid id) => await client.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{id}");

    [Fact]
    public async Task UnCreador_CreaElCasoConTodoLoConfigurado_YElTituloSeEscribeSolo()
    {
        var e = await CrearEntornoAsync();

        var respuesta = await e.Admin.PostAsJsonAsync($"/api/v1/creadores-de-caso/{e.CreadorId}/casos", new { titulo = (string?)null, datosJson = """{"cliente":"ACME"}""" });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var creado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal($"Alta de cliente {DateTimeOffset.Now:yyyy-MM-dd} #1", creado.GetProperty("titulo").GetString());
        Assert.Equal("Alta", creado.GetProperty("tipoCaso").GetString());
        Assert.Equal("NUEVO", creado.GetProperty("estadoNegocio").GetProperty("codigo").GetString());

        // The step comes from the creator by name: the first one, "Validar", is skipped, as when picking the starting step by hand.
        var caso = await CasoAsync(e.Admin, creado.GetProperty("id").GetGuid());
        Assert.Equal("ACME", JsonDocument.Parse(caso.GetProperty("datosJson").GetString()!).RootElement.GetProperty("cliente").GetString());
        var casoId = creado.GetProperty("id").GetGuid();
        var ejecuciones = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}/ejecuciones");
        var ejecucion = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}/ejecuciones/{ejecuciones.EnumerateArray().Single().GetProperty("id").GetGuid()}");
        var pasos = ejecucion.GetProperty("pasos").EnumerateArray().Select(p => p.GetProperty("estado").GetString()).ToList();
        Assert.Contains("Omitido", pasos);
    }

    [Fact]
    public async Task ElNumeroDelTitulo_AvanzaConCadaCaso_AunqueSeCreenAlMismoTiempo()
    {
        var e = await CrearEntornoAsync("{creador} #{n}");

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            e.Admin.PostAsJsonAsync($"/api/v1/creadores-de-caso/{e.CreadorId}/casos", new { titulo = (string?)null, datosJson = """{"cliente":"ACME"}""" })));

        var titulos = new List<string>();
        foreach (var respuesta in respuestas)
        {
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            titulos.Add((await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("titulo").GetString()!);
        }

        Assert.Equal(Enumerable.Range(1, 8).Select(n => $"Alta de cliente #{n}").Order(), titulos.Order());
    }

    [Fact]
    public async Task ElUsuarioPuedeEscribirElTitulo_YEntoncesNoSeGastaNumero()
    {
        var e = await CrearEntornoAsync("{creador} #{n}");

        var propio = await (await e.Admin.PostAsJsonAsync($"/api/v1/creadores-de-caso/{e.CreadorId}/casos", new { titulo = "  Expediente 2026-014  ", datosJson = """{"cliente":"ACME"}""" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var automatico = await (await e.Admin.PostAsJsonAsync($"/api/v1/creadores-de-caso/{e.CreadorId}/casos", new { titulo = "", datosJson = """{"cliente":"ACME"}""" }))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Expediente 2026-014", propio.GetProperty("titulo").GetString());
        Assert.Equal("Alta de cliente #1", automatico.GetProperty("titulo").GetString());
    }

    [Fact]
    public async Task ElTituloPuedeLlevarDatosDelCaso()
    {
        var e = await CrearEntornoAsync("{datos.cliente} · {tipo}");

        var creado = await (await e.Admin.PostAsJsonAsync($"/api/v1/creadores-de-caso/{e.CreadorId}/casos", new { titulo = (string?)null, datosJson = """{"cliente":"ACME"}""" }))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("ACME · Alta", creado.GetProperty("titulo").GetString());
    }

    [Fact]
    public async Task LosDatosSeValidanContraElEsquemaDelTipoDelCreador()
    {
        var e = await CrearEntornoAsync();

        var sinCliente = await e.Admin.PostAsJsonAsync($"/api/v1/creadores-de-caso/{e.CreadorId}/casos", new { titulo = (string?)null, datosJson = "{}" });

        Assert.NotEqual(HttpStatusCode.OK, sinCliente.StatusCode);
        Assert.True((int)sinCliente.StatusCode is >= 400 and < 500);
    }

    [Fact]
    public async Task LaLista_OfreceSoloLosCreadoresActivosDeProcesosAsignados_ConElFormularioYUnTituloDeEjemplo()
    {
        var e = await CrearEntornoAsync("{creador} #{n}");
        var retirado = await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/creadores", new
        {
            nombre = "Retirado", tipoCasoId = (Guid?)null, pasoInicialNombre = (string?)null, estadoNegocioInicialId = (Guid?)null,
            plantillaTitulo = (string?)null, orden = 2, activo = false,
        }));

        var lista = await e.Admin.GetFromJsonAsync<JsonElement>("/api/v1/creadores-de-caso");

        var suyo = lista.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == e.CreadorId);
        Assert.Equal("Alta", suyo.GetProperty("tipoCasoNombre").GetString());
        Assert.Contains("\"cliente\"", suyo.GetProperty("esquemaDatosJson").GetString());
        Assert.Equal("Alta de cliente #1", suyo.GetProperty("tituloEjemplo").GetString());
        Assert.DoesNotContain(lista.EnumerateArray(), c => c.GetProperty("id").GetGuid() == retirado);
    }

    [Fact]
    public async Task UnUsuarioSinAsignacion_NoVeNiPuedeUsarElCreador()
    {
        var e = await CrearEntornoAsync();
        var (otro, _) = await UsuarioAsync(SystemRoles.Admin);

        var lista = await otro.GetFromJsonAsync<JsonElement>("/api/v1/creadores-de-caso");
        var intento = await otro.PostAsJsonAsync($"/api/v1/creadores-de-caso/{e.CreadorId}/casos", new { titulo = (string?)null, datosJson = """{"cliente":"x"}""" });

        Assert.DoesNotContain(lista.EnumerateArray(), c => c.GetProperty("id").GetGuid() == e.CreadorId);
        Assert.Equal(HttpStatusCode.NotFound, intento.StatusCode);
    }

    [Fact]
    public async Task SinElPermisoDeCrear_NoSeAbreNingunaPuerta()
    {
        var e = await CrearEntornoAsync();
        var (sinPermisos, _) = await UsuarioAsync(null);

        Assert.Equal(HttpStatusCode.Forbidden, (await sinPermisos.GetAsync("/api/v1/creadores-de-caso")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await sinPermisos.PostAsJsonAsync($"/api/v1/creadores-de-caso/{e.CreadorId}/casos", new { titulo = "x", datosJson = (string?)null })).StatusCode);
    }

    [Fact]
    public async Task AlGuardarUnCreador_SeRechazaLoQueNoEncaja()
    {
        var e = await CrearEntornoAsync();
        var otroFlujo = await IdAsync(await e.Admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Otro {Guid.NewGuid():N}", descripcion = (string?)null }));
        object Cuerpo(string nombre, Guid? tipo = null, string? paso = null, string? plantilla = null) =>
            new { nombre, tipoCasoId = tipo, pasoInicialNombre = paso, estadoNegocioInicialId = (Guid?)null, plantillaTitulo = plantilla, orden = 3 };

        // A type of another process, a step the active version does not have, a typo in the template, a repeated name.
        Assert.Equal(HttpStatusCode.Conflict, (await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{otroFlujo}/creadores", Cuerpo("A", tipo: e.TipoCasoId))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/creadores", Cuerpo("B", paso: "No existe"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/creadores", Cuerpo("C", plantilla: "{fehca}"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/creadores", Cuerpo("Alta de cliente"))).StatusCode);
    }

    [Fact]
    public async Task EditarYBorrarUnCreador_SeVeEnLaLista_YLosCasosYaCreadosNoCambian()
    {
        var e = await CrearEntornoAsync();
        var creado = await (await e.Admin.PostAsJsonAsync($"/api/v1/creadores-de-caso/{e.CreadorId}/casos", new { titulo = (string?)null, datosJson = """{"cliente":"ACME"}""" }))
            .Content.ReadFromJsonAsync<JsonElement>();

        var cambio = await e.Admin.PutAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/creadores/{e.CreadorId}", new
        {
            nombre = "Alta express", descripcion = (string?)null, tipoCasoId = (Guid?)null, pasoInicialNombre = (string?)null,
            estadoNegocioInicialId = (Guid?)null, plantillaTitulo = (string?)null, orden = 1, activo = true,
        });
        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);
        var editado = await cambio.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Alta express", editado.GetProperty("nombre").GetString());
        Assert.Equal(JsonValueKind.Null, editado.GetProperty("tipoCasoId").ValueKind);
        Assert.Equal("{creador} {fecha} #{n}", editado.GetProperty("plantillaTitulo").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.DeleteAsync($"/api/v1/flujos/{e.FlujoId}/creadores/{e.CreadorId}")).StatusCode);
        var lista = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/creadores");
        Assert.Empty(lista.EnumerateArray());
        Assert.Equal("Alta", (await CasoAsync(e.Admin, creado.GetProperty("id").GetGuid())).GetProperty("tipoCaso").GetString());
    }
}
