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
/// The parameters of a process that the people working it can change from the dashboard: only the ones the author opened up, only
/// on processes they are assigned to, and the robot reads what they wrote.
/// </summary>
public sealed class ParametrosEditablesFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private sealed record Entorno(HttpClient Admin, Guid UserId, Guid FlujoId, string ApiKey, Guid IvaId, Guid CuponId, Guid SelectorId);

    private async Task<(HttpClient Client, Guid UserId)> UsuarioAsync(string? rol)
    {
        var client = factory.CreateClient();
        var email = $"param-ed-{Guid.NewGuid():N}@example.com";

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

    private async Task<Entorno> CrearEntornoAsync()
    {
        var (admin, userId) = await UsuarioAsync(SystemRoles.Admin);

        var equipoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/equipos", new { nombre = $"Equipo {Guid.NewGuid():N}", descripcion = (string?)null }));
        var servicioId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Servicio {Guid.NewGuid():N}", descripcion = (string?)null }));
        var flujoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Proceso {Guid.NewGuid():N}", descripcion = (string?)null }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/asignaciones", new { userId })).StatusCode);

        var despliegue = await admin.PostAsJsonAsync("/api/v1/despliegues", new { equipoId, servicioId, flujoId });
        var apiKey = (await despliegue.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("apiKey").GetString()!;

        var iva = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/parametros", new { codigo = "iva", valor = "21", descripcion = "Porcentaje de IVA", editablePorUsuario = true, etiqueta = "IVA (%)" }));
        var cupon = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/parametros", new { codigo = "cupon", valor = "", descripcion = (string?)null, editablePorUsuario = true, etiqueta = (string?)null }));
        var selector = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/parametros", new { codigo = "selector_boton", valor = "#buy", descripcion = "Solo para quien gestiona el proceso" }));

        return new Entorno(admin, userId, flujoId, apiKey, iva, cupon, selector);
    }

    private static Task<HttpResponseMessage> Guardar(HttpClient client, Guid flujoId, params (Guid Id, string Valor)[] valores) =>
        client.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/parametros-editables", new { valores = valores.Select(v => new { id = v.Id, valor = v.Valor }) });

    [Fact]
    public async Task LaPersonaSoloVeLosParametrosAbiertos_ConSuEtiqueta()
    {
        var e = await CrearEntornoAsync();

        var lista = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/parametros-editables");

        var vistos = lista.EnumerateArray().Select(p => (Codigo: p.GetProperty("codigo").GetString(), Etiqueta: p.GetProperty("etiqueta").GetString())).ToList();
        Assert.Equal([("cupon", "cupon"), ("iva", "IVA (%)")], vistos.OrderBy(v => v.Codigo).ToList());
        Assert.DoesNotContain(vistos, v => v.Codigo == "selector_boton");
    }

    [Fact]
    public async Task LoQueEscribeLaPersona_LoLeeElRobot()
    {
        var e = await CrearEntornoAsync();

        var guardado = await Guardar(e.Admin, e.FlujoId, (e.IvaId, "10"), (e.CuponId, "VERANO"));

        Assert.Equal(HttpStatusCode.OK, guardado.StatusCode);
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", e.ApiKey);
        var parametros = await new RpaClient(http).ObtenerParametrosAsync();
        Assert.Equal("10", parametros.Texto("iva"));
        Assert.Equal("VERANO", parametros.Texto("cupon"));
        Assert.Equal("#buy", parametros.Texto("selector_boton"));
    }

    [Fact]
    public async Task UnParametroQueNoSeAbrio_NoSePuedeCambiarDesdeElPanel_YNoSeCambiaNada()
    {
        var e = await CrearEntornoAsync();

        var guardado = await Guardar(e.Admin, e.FlujoId, (e.IvaId, "7"), (e.SelectorId, "#otro"));

        Assert.Equal(HttpStatusCode.Conflict, guardado.StatusCode);
        var lista = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/parametros");
        Assert.Equal("21", lista.EnumerateArray().Single(p => p.GetProperty("codigo").GetString() == "iva").GetProperty("valor").GetString());
        Assert.Equal("#buy", lista.EnumerateArray().Single(p => p.GetProperty("codigo").GetString() == "selector_boton").GetProperty("valor").GetString());
    }

    [Fact]
    public async Task SinElPermisoOSinAsignacion_NoSeAbreLaPuerta()
    {
        var e = await CrearEntornoAsync();
        var (sinPermiso, _) = await UsuarioAsync(null);
        var (otroAdmin, _) = await UsuarioAsync(SystemRoles.Admin);

        Assert.Equal(HttpStatusCode.Forbidden, (await sinPermiso.GetAsync($"/api/v1/flujos/{e.FlujoId}/parametros-editables")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Guardar(sinPermiso, e.FlujoId, (e.IvaId, "1"))).StatusCode);
        // An administrator who is not assigned to the process gets the same answer as for one that does not exist.
        Assert.Equal(HttpStatusCode.NotFound, (await otroAdmin.GetAsync($"/api/v1/flujos/{e.FlujoId}/parametros-editables")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Guardar(otroAdmin, e.FlujoId, (e.IvaId, "1"))).StatusCode);
    }

    [Fact]
    public async Task ElValorNoSePuedeDejarSinMandar_NiRepetirseUnParametro()
    {
        var e = await CrearEntornoAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await Guardar(e.Admin, e.FlujoId)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Guardar(e.Admin, e.FlujoId, (e.IvaId, "1"), (e.IvaId, "2"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Guardar(e.Admin, e.FlujoId, (e.IvaId, new string('x', 2001)))).StatusCode);
    }

    [Fact]
    public async Task EditarUnParametroComoAdministrador_SinMencionarLoDelPanel_NoLoCierra()
    {
        var e = await CrearEntornoAsync();

        // A client that predates the two new fields: it only sends the value and the description.
        var cambio = await e.Admin.PatchAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/parametros/{e.IvaId}", new { valor = "19", descripcion = "IVA" });

        Assert.Equal(HttpStatusCode.OK, cambio.StatusCode);
        var cuerpo = await cambio.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(cuerpo.GetProperty("editablePorUsuario").GetBoolean());
        Assert.Equal("IVA (%)", cuerpo.GetProperty("etiqueta").GetString());

        var cierre = await e.Admin.PatchAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/parametros/{e.IvaId}", new { valor = "19", descripcion = "IVA", editablePorUsuario = false, etiqueta = "" });
        var cerrado = await cierre.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(cerrado.GetProperty("editablePorUsuario").GetBoolean());
        Assert.Equal(JsonValueKind.Null, cerrado.GetProperty("etiqueta").ValueKind);
        var lista = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/parametros-editables");
        Assert.DoesNotContain(lista.EnumerateArray(), p => p.GetProperty("codigo").GetString() == "iva");
    }

    [Fact]
    public async Task ElResumenDelPanel_DiceCuantosParametrosSePuedenCambiar()
    {
        var e = await CrearEntornoAsync();

        var resumen = await e.Admin.GetFromJsonAsync<JsonElement>("/api/v1/casos/resumen");

        var tarjeta = resumen.EnumerateArray().Single(r => r.GetProperty("flujoId").GetGuid() == e.FlujoId);
        Assert.Equal(2, tarjeta.GetProperty("parametrosEditables").GetInt32());
    }
}
