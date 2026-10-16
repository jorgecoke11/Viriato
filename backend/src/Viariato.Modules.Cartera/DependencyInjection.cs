using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.Cartera.Persistence;

namespace Viariato.Modules.Cartera;

public static class DependencyInjection
{
    /// <summary>The simulated portfolio of the trading robots: its tables, and nothing else to configure.</summary>
    public static IServiceCollection AddCarteraModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModuleModelConfiguration, CarteraModuleModelConfiguration>();
        return services;
    }
}
