using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>
/// Boots the real Api host against an ephemeral Postgres container. Migrations and the
/// permission/role seeder run exactly as they do in Development, via Program.cs.
/// </summary>
public sealed class ViariatoApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("viariato_test")
        .WithUsername("viariato")
        .WithPassword("viariato_test_password")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                ["Jwt:Issuer"] = "viariato.tests",
                ["Jwt:Audience"] = "viariato.tests",
                ["Jwt:SigningKey"] = new string('t', 32),
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Auth:RefreshTokenDays"] = "30",
                ["Credenciales:ClaveCifrado"] = Convert.ToBase64String(new byte[32].Select((_, i) => (byte)(i + 1)).ToArray()),
                // Same 15-minute window as production, but a high ceiling so a multi-step
                // integration flow doesn't trip the very rate limiting it's meant to exercise.
                ["RateLimiting:Auth:PermitLimit"] = "1000",
                ["RateLimiting:Auth:WindowMinutes"] = "15",
                ["RateLimiting:Refresh:PermitLimit"] = "1000",
                // The background sweep that cancels overdue steps is run by hand in the tests that need it: left to its
                // own schedule it could cancel a Caso in the middle of a test that is looking at it overdue.
                ["Despacho:BarridoSegundos"] = "3600",
                // Same for the launchers: the tests that need one turn of the clock run it by hand with a clock of their own.
                ["Despacho:LanzadoresSegundos"] = "3600",
                // And the reprocessor: the tests run its turns by hand.
                ["Despacho:ReprocesamientosSegundos"] = "3600",
                // And the postman of the notifications: the tests send one turn by hand.
                ["Notificaciones:SegundosEntreRondas"] = "3600",
                ["Notificaciones:UrlPublica"] = "https://viriato.example",
            });
        });
    }

    public Task InitializeAsync() => _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
