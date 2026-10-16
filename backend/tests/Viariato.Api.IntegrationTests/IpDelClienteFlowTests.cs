using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>
/// Behind Caddy and Cloudflare Tunnel every request reaches the API from Caddy's container. The limits must still count each client
/// apart — by the IP the tunnel reports in X-Forwarded-For — or a few reloads between several people log everyone out.
/// </summary>
public sealed class IpDelClienteFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private const string Cloudflared = "172.18.0.3";

    /// <summary>Every connection comes from "Caddy" (a private address), as in production.</summary>
    private sealed class DesdeCaddy : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, siguiente) =>
            {
                context.Connection.RemoteIpAddress = IPAddress.Parse("172.18.0.5");
                return siguiente(context);
            });
            next(app);
        };
    }

    private WebApplicationFactory<Program> TrasElTunel() => factory.WithWebHostBuilder(builder =>
    {
        // The limits are read while the services are registered, before any ConfigureAppConfiguration of a test applies: UseSetting is.
        builder.UseSetting("RateLimiting:Auth:PermitLimit", "3");
        builder.UseSetting("RateLimiting:Refresh:PermitLimit", "1000");
        builder.ConfigureServices(services => services.AddTransient<IStartupFilter, DesdeCaddy>());
    });

    private static async Task<HttpStatusCode> EntrarAsync(HttpClient client, string reenviadoPor, string email)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password = "contraseña-equivocada" }),
        };
        request.Headers.Add("X-Forwarded-For", reenviadoPor);
        return (await client.SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task Cada_cliente_tiene_sus_intentos_aunque_todos_lleguen_desde_Caddy()
    {
        using var host = TrasElTunel();
        var client = host.CreateClient();
        var email = $"ip-{Guid.NewGuid():N}@example.com";

        for (var i = 0; i < 3; i++) Assert.Equal(HttpStatusCode.Unauthorized, await EntrarAsync(client, $"203.0.113.10, {Cloudflared}", email));
        Assert.Equal(HttpStatusCode.TooManyRequests, await EntrarAsync(client, $"203.0.113.10, {Cloudflared}", email));

        // Someone else, through the same Caddy and the same tunnel, still has all their attempts.
        Assert.Equal(HttpStatusCode.Unauthorized, await EntrarAsync(client, $"203.0.113.20, {Cloudflared}", email));
    }

    [Fact]
    public async Task Inventarse_una_IP_a_la_izquierda_no_da_intentos_nuevos()
    {
        using var host = TrasElTunel();
        var client = host.CreateClient();
        var email = $"ip-{Guid.NewGuid():N}@example.com";

        for (var i = 0; i < 3; i++) Assert.Equal(HttpStatusCode.Unauthorized, await EntrarAsync(client, $"203.0.113.30, {Cloudflared}", email));

        // Cloudflare appends the real client after whatever the client sent: the API reads from the right, so it is still the same one.
        Assert.Equal(HttpStatusCode.TooManyRequests, await EntrarAsync(client, $"198.51.100.99, 203.0.113.30, {Cloudflared}", email));
    }

    [Fact]
    public async Task Renovar_la_sesion_tiene_su_propio_limite_y_no_gasta_los_intentos_de_entrar()
    {
        using var host = TrasElTunel();
        var client = host.CreateClient();

        // Many more renewals than login attempts (3 here): every page load does one, and they are not guessing anything.
        for (var i = 0; i < 10; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
            request.Headers.Add("X-Forwarded-For", $"203.0.113.40, {Cloudflared}");
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, await EntrarAsync(client, $"203.0.113.40, {Cloudflared}", $"ip-{Guid.NewGuid():N}@example.com"));
    }
}
