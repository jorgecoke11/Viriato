using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.Notificaciones.Endpoints;
using Viariato.Modules.Notificaciones.Envio;
using Viariato.Modules.Notificaciones.Persistence;

namespace Viariato.Modules.Notificaciones;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificacionesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModuleModelConfiguration, NotificacionesModuleModelConfiguration>();
        services.Configure<NotificacionesOptions>(configuration.GetSection(NotificacionesOptions.SectionName));

        // Rules fire as the case's history is saved (see DisparadorDeNotificaciones), and the postman sends what they queued.
        services.AddScoped<ISaveChangesInterceptor, DisparadorDeNotificaciones>();
        services.AddScoped<BandejaDeSalida>();
        services.AddHostedService<CarteroDeNotificaciones>();
        return services;
    }

    public static IEndpointRouteBuilder MapNotificacionesModuleEndpoints(this IEndpointRouteBuilder endpoints) => endpoints.MapNotificacionesEndpoints();
}
