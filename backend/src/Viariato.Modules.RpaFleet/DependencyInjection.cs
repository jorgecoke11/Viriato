using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.RpaFleet.Auth;
using Viariato.Modules.RpaFleet.Contracts;
using Viariato.Modules.RpaFleet.Persistence;
using Viariato.Modules.RpaFleet.Validation;

namespace Viariato.Modules.RpaFleet;

public static class DependencyInjection
{
    public static IServiceCollection AddRpaFleetModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModuleModelConfiguration, RpaFleetModuleModelConfiguration>();
        services.AddScoped<IDespliegueApiKeyValidator, DespliegueApiKeyValidator>();

        services.AddSingleton<IValidator<CreateEquipoRequest>, CreateEquipoRequestValidator>();
        services.AddSingleton<IValidator<UpdateEquipoRequest>, UpdateEquipoRequestValidator>();
        services.AddSingleton<IValidator<CreateServicioRequest>, CreateServicioRequestValidator>();
        services.AddSingleton<IValidator<UpdateServicioRequest>, UpdateServicioRequestValidator>();
        services.AddSingleton<CreateDespliegueRequestValidator>();

        return services;
    }
}
