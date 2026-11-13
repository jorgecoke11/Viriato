using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure.BackgroundQueue;
using Viariato.Infrastructure.Trabajos;
using Viariato.Shared.Options;
using Viariato.Shared.Seguridad;

namespace Viariato.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Reads the connection string lazily, when the DbContext is first resolved rather than
        // at registration time, so hosts that mutate configuration after this call (as
        // WebApplicationFactory-based tests do, right before Build()) are picked up correctly.
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var connectionString = configuration.GetConnectionString("Postgres")
                ?? throw new InvalidOperationException("Missing configuration: ConnectionStrings:Postgres.");

            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();

            // A module that has to react to what is saved, in the same transaction (notifications to what happens to a case), registers
            // an ISaveChangesInterceptor; this is where they all join the context.
            options.AddInterceptors(serviceProvider.GetServices<ISaveChangesInterceptor>());
        });

        // Encrypts the secrets modules keep in the database (robot credentials, connection passwords). Fails the API at
        // startup, not at the first secret saved, if the master key is missing or malformed.
        services.AddOptions<CredencialesOptions>()
            .Bind(configuration.GetSection(CredencialesOptions.SectionName))
            .Validate(o => CredencialesOptions.EsClaveValida(o.ClaveCifrado), CredencialesOptions.MensajeClaveInvalida)
            .ValidateOnStart();
        services.AddSingleton<ICredencialProtector, AesGcmCredencialProtector>();

        services.AddScoped<ITrabajoTracker, TrabajoTracker>();
        services.AddSingleton<IBackgroundTaskQueue, BackgroundTaskQueue>();
        services.AddHostedService<QueuedHostedService>();

        return services;
    }
}
