using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure.BackgroundQueue;
using Viariato.Infrastructure.Trabajos;

namespace Viariato.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Reads the connection string lazily, when the DbContext is first resolved rather than
        // at registration time, so hosts that mutate configuration after this call (as
        // WebApplicationFactory-based tests do, right before Build()) are picked up correctly.
        services.AddDbContext<AppDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException("Missing configuration: ConnectionStrings:Postgres.");

            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
        });

        services.AddScoped<ITrabajoTracker, TrabajoTracker>();
        services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
        services.AddHostedService<QueuedHostedService>();

        return services;
    }
}
