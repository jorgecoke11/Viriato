using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.Users.Domain;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>
/// The door of the robot's live screen (/vista/ in Caddy): a person with casos.read gets a pass in a cookie only sent to /vista, and
/// the check Caddy makes before each request lets through that cookie and nothing else.
/// </summary>
public sealed class VistaEnDirectoFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    // Cookies are read and sent by hand: the pass is scoped to /vista, and Caddy forwards it to the check as a plain header.
    private HttpClient Cliente() => factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    private async Task<HttpClient> UsuarioAsync(bool sinPermisos = false)
    {
        var client = Cliente();
        var email = $"vista-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "SuperSecret123", displayName = "Prueba" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        if (sinPermisos)
        {
            var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("id").GetGuid();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RemoveRange(await db.Set<UserRole>().Where(ur => ur.UserId == userId).ToListAsync());
            await db.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "SuperSecret123" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string CookieDeVista(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        return Assert.Single(cookies, c => c.StartsWith("viriato_vista=", StringComparison.Ordinal));
    }

    private async Task<HttpStatusCode> ComprobarAsync(string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/casos/vista/comprobar");
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
        return (await Cliente().SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task Quien_puede_ver_casos_recibe_un_pase_que_abre_la_vista()
    {
        var client = await UsuarioAsync();

        var response = await client.PostAsync("/api/v1/casos/vista/pase", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var setCookie = CookieDeVista(response);
        Assert.Contains("path=/vista", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);

        var cookie = setCookie.Split(';')[0];
        Assert.Equal(HttpStatusCode.NoContent, await ComprobarAsync(cookie));
    }

    [Fact]
    public async Task Sin_pase_o_con_uno_tocado_la_vista_no_se_abre()
    {
        var pase = CookieDeVista(await (await UsuarioAsync()).PostAsync("/api/v1/casos/vista/pase", null)).Split(';')[0];
        var tocado = pase[..^1] + (pase[^1] == '0' ? '1' : '0');

        Assert.Equal(HttpStatusCode.Unauthorized, await ComprobarAsync(null));
        Assert.Equal(HttpStatusCode.Unauthorized, await ComprobarAsync("viriato_vista=basura"));
        Assert.Equal(HttpStatusCode.Unauthorized, await ComprobarAsync(tocado));
    }

    [Fact]
    public async Task Sin_sesion_o_sin_permiso_para_ver_casos_no_hay_pase()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Cliente().PostAsync("/api/v1/casos/vista/pase", null)).StatusCode);

        var sinPermisos = await UsuarioAsync(sinPermisos: true);
        Assert.Equal(HttpStatusCode.Forbidden, (await sinPermisos.PostAsync("/api/v1/casos/vista/pase", null)).StatusCode);
    }

    [Fact]
    public async Task Un_pase_no_sirve_como_token_de_la_api()
    {
        var pase = CookieDeVista(await (await UsuarioAsync()).PostAsync("/api/v1/casos/vista/pase", null)).Split(';')[0]["viriato_vista=".Length..];

        var client = Cliente();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pase);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/casos")).StatusCode);
    }
}
