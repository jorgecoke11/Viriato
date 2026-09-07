using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.Flujos.Persistence;
using Viariato.Modules.Flujos.Validation;

namespace Viariato.Modules.Flujos;

public static class DependencyInjection
{
    public static IServiceCollection AddFlujosModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModuleModelConfiguration, FlujosModuleModelConfiguration>();

        services.AddSingleton<CreateFlujoRequestValidator>();
        services.AddSingleton<UpdateFlujoRequestValidator>();
        services.AddSingleton<CreateFlujoVersionRequestValidator>();
        services.AddSingleton<ReplacePasosRequestValidator>();
        services.AddSingleton<CreateFlujoEstadoDefRequestValidator>();
        services.AddSingleton<UpdateFlujoEstadoDefRequestValidator>();
        services.AddSingleton<CreateFlujoTipoCasoRequestValidator>();
        services.AddSingleton<UpdateFlujoTipoCasoRequestValidator>();
        services.AddSingleton<CreateAgenteDefinicionRequestValidator>();
        services.AddSingleton<UpdateAgenteDefinicionRequestValidator>();
        services.AddSingleton<CreateStorageConfigRequestValidator>();
        services.AddSingleton<UpdateStorageConfigRequestValidator>();

        return services;
    }
}
