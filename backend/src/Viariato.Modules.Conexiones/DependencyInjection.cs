using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.Conexiones.Endpoints;
using Viariato.Modules.Conexiones.Persistence;
using Viariato.Modules.Conexiones.Tipos;

namespace Viariato.Modules.Conexiones;

public static class DependencyInjection
{
    public static IServiceCollection AddConexionesModule(this IServiceCollection services)
    {
        services.AddSingleton<IModuleModelConfiguration, ConexionesModuleModelConfiguration>();

        // The kinds of account: a new one (Telegram, an API…) is one more line here.
        services.AddSingleton<ITipoDeConexion, ConexionDeCorreo>();
        services.AddSingleton<TiposDeConexion>();

        services.AddScoped<EnviadorDeMensajes>();
        return services;
    }

    public static IEndpointRouteBuilder MapConexionesModuleEndpoints(this IEndpointRouteBuilder endpoints) => endpoints.MapConexionesEndpoints();
}
