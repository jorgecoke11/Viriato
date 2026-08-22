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
                // Same 15-minute window as production, but a high ceiling so a multi-step
                // integration flow doesn't trip the very rate limiting it's meant to exercise.
                ["RateLimiting:Auth:PermitLimit"] = "1000",
                ["RateLimiting:Auth:WindowMinutes"] = "15",
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
