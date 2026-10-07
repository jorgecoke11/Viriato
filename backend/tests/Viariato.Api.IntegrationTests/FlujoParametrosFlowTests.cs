using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Authorization;
using Viriato.Rpa.Client;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>
/// Per-process settings end to end: an admin edits them through the Flujo's API, and a robot reads back
/// exactly its own process's settings with the Viriato.Rpa.Client library — never another process's.
/// </summary>
public sealed class FlujoParametrosFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private sealed record Entorno(HttpClient Admin, Guid FlujoA, Guid FlujoB, string KeyA);

    private async Task<Entorno> CrearEntornoAsync()
    {
        var admin = factory.CreateClient();
        var email = $"param-{Guid.NewGuid():N}@example.com";

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
        var flujoA = await IdAsync(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Proceso A {Guid.NewGuid():N}", descripcion = (string?)null }));
        var flujoB = await IdAsync(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Proceso B {Guid.NewGuid():N}", descripcion = (string?)null }));

        var despliegue = await admin.PostAsJsonAsync("/api/v1/despliegues", new { equipoId, servicioId, flujoId = flujoA });
        Assert.Equal(HttpStatusCode.OK, despliegue.StatusCode);
        var keyA = (await despliegue.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("apiKey").GetString()!;

        return new Entorno(admin, flujoA, flujoB, keyA);
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> Crear(HttpClient admin, Guid flujoId, string codigo, string? valor, string? descripcion = null) =>
        admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/parametros", new { codigo, valor, descripcion });

    private RpaClient Robot(string apiKey)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return new RpaClient(http);
    }

    [Fact]
    public async Task AnAdmin_CreatesListsUpdatesAndDeletesSettings()
    {
        var e = await CrearEntornoAsync();

        var iva = await Crear(e.Admin, e.FlujoA, "iva", "21", "Porcentaje de IVA");
        await Crear(e.Admin, e.FlujoA, "n_maximo_carrito", "14");
        var id = await IdAsync(iva);

        var listado = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoA}/parametros");
        Assert.Equal(["iva", "n_maximo_carrito"], listado.EnumerateArray().Select(p => p.GetProperty("codigo").GetString()!).ToArray());
        Assert.Equal("Porcentaje de IVA", listado[0].GetProperty("descripcion").GetString());

        var actualizado = await e.Admin.PatchAsJsonAsync($"/api/v1/flujos/{e.FlujoA}/parametros/{id}", new { valor = "10", descripcion = (string?)null });
        Assert.Equal(HttpStatusCode.OK, actualizado.StatusCode);
        var cuerpo = await actualizado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("10", cuerpo.GetProperty("valor").GetString());
        Assert.Equal("iva", cuerpo.GetProperty("codigo").GetString());
        Assert.Equal(JsonValueKind.Null, cuerpo.GetProperty("descripcion").ValueKind);

        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.DeleteAsync($"/api/v1/flujos/{e.FlujoA}/parametros/{id}")).StatusCode);
        var tras = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoA}/parametros");
        Assert.Equal(1, tras.GetArrayLength());
    }

    [Fact]
    public async Task InvalidRequests_AreRejected_WithTheRightStatus()
    {
        var e = await CrearEntornoAsync();
        Assert.Equal(HttpStatusCode.OK, (await Crear(e.Admin, e.FlujoA, "iva", "21")).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await Crear(e.Admin, e.FlujoA, "IVA-Mayusculas", "21")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Crear(e.Admin, e.FlujoA, "con espacios", "21")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Crear(e.Admin, e.FlujoA, "", "21")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Crear(e.Admin, e.FlujoA, "sin-valor", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Crear(e.Admin, e.FlujoA, "iva", "99")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Crear(e.Admin, Guid.NewGuid(), "iva", "21")).StatusCode);
    }

    [Fact]
    public async Task AnEmptyValue_IsALegitimateSetting()
    {
        var e = await CrearEntornoAsync();

        Assert.Equal(HttpStatusCode.OK, (await Crear(e.Admin, e.FlujoA, "cupon", "")).StatusCode);

        var parametros = await Robot(e.KeyA).ObtenerParametrosAsync();
        Assert.Equal("", parametros.Texto("cupon"));
    }

    [Fact]
    public async Task TheSameCodeCanExistInDifferentProcesses()
    {
        var e = await CrearEntornoAsync();

        Assert.Equal(HttpStatusCode.OK, (await Crear(e.Admin, e.FlujoA, "iva", "21")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Crear(e.Admin, e.FlujoB, "iva", "10")).StatusCode);
    }

    [Fact]
    public async Task ASetting_CannotBeTouchedThroughAnotherProcess()
    {
        var e = await CrearEntornoAsync();
        var idDeA = await IdAsync(await Crear(e.Admin, e.FlujoA, "iva", "21"));

        var editar = await e.Admin.PatchAsJsonAsync($"/api/v1/flujos/{e.FlujoB}/parametros/{idDeA}", new { valor = "0", descripcion = (string?)null });
        var borrar = await e.Admin.DeleteAsync($"/api/v1/flujos/{e.FlujoB}/parametros/{idDeA}");

        Assert.Equal(HttpStatusCode.NotFound, editar.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, borrar.StatusCode);
        Assert.Equal("21", (await Robot(e.KeyA).ObtenerParametrosAsync()).Texto("iva"));
    }

    [Fact]
    public async Task ARobot_ReadsExactlyItsOwnProcessSettings_TypedAndNeverAnotherProcesses()
    {
        var e = await CrearEntornoAsync();
        await Crear(e.Admin, e.FlujoA, "iva", "21");
        await Crear(e.Admin, e.FlujoA, "n_maximo_carrito", "14");
        await Crear(e.Admin, e.FlujoB, "iva", "10");
        await Crear(e.Admin, e.FlujoB, "solo-del-otro", "secreto-del-otro");

        var parametros = await Robot(e.KeyA).ObtenerParametrosAsync();

        Assert.Equal(21m, parametros.DecimalObligatorio("IVA"));
        Assert.Equal(14, parametros.EnteroObligatorio("n_maximo_carrito"));
        Assert.Equal(2, parametros.Valores.Count);
        Assert.False(parametros.Contiene("solo-del-otro"));
    }

    [Fact]
    public async Task AProcessWithoutSettings_GivesAnEmptySet_AndAMissingRequiredOneSaysSo()
    {
        var e = await CrearEntornoAsync();

        var parametros = await Robot(e.KeyA).ObtenerParametrosAsync();

        Assert.Empty(parametros.Valores);
        var ex = Assert.Throws<InvalidOperationException>(() => parametros.EnteroObligatorio("n_maximo_carrito"));
        Assert.Contains("'n_maximo_carrito'", ex.Message);
    }

    [Fact]
    public async Task OnlyARobotKeyOpensTheRobotEndpoint_AndOnlyAnAdminCanEdit()
    {
        var e = await CrearEntornoAsync();

        var sinClave = await Assert.ThrowsAsync<ViriatoApiException>(() => Robot("rpa_clave-que-no-existe").ObtenerParametrosAsync());
        Assert.True(sinClave.IsUnauthorized);
        Assert.Equal(HttpStatusCode.Unauthorized, (await e.Admin.GetAsync("/api/v1/rpa/parametros")).StatusCode);

        var robot = factory.CreateClient();
        robot.DefaultRequestHeaders.Add("X-Api-Key", e.KeyA);
        Assert.Equal(HttpStatusCode.Unauthorized, (await robot.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoA}/parametros", new { codigo = "x", valor = "1" })).StatusCode);
    }
}
