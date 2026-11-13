using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Viariato.Infrastructure;
using Viariato.Modules.RpaFleet.Auth;
using Viariato.Modules.RpaFleet.Contracts;
using Viariato.Modules.RpaFleet.Lanzador;
using Viariato.Modules.RpaFleet.Options;
using Viariato.Modules.RpaFleet.Persistence;
using Viariato.Modules.RpaFleet.Security;
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
        services.AddSingleton<UpdateDespliegueRequestValidator>();
        services.AddSingleton<IValidator<UpdateDespachoEquipoRequest>, UpdateDespachoEquipoRequestValidator>();
        services.AddSingleton<IValidator<SavePlantillaDespachoRequest>, SavePlantillaDespachoRequestValidator>();
        services.AddSingleton<CreateCredencialRequestValidator>();
        services.AddSingleton<UpdateCredencialRequestValidator>();

        // Fails the API at startup, not at the first credential saved, if the master key is missing or malformed.
        services.AddOptions<CredencialesOptions>()
            .Bind(configuration.GetSection(CredencialesOptions.SectionName))
            .Validate(o => CredencialesOptions.EsClaveValida(o.ClaveCifrado), CredencialesOptions.MensajeClaveInvalida)
            .ValidateOnStart();
        services.AddSingleton<ICredencialProtector, AesGcmCredencialProtector>();

        // The launcher next to Viriato: the secret it shares with it (created at startup, so the launcher finds it) and where the
        // robots it runs are kept.
        services.AddOptions<LanzadorOptions>().Bind(configuration.GetSection(LanzadorOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<SecretoDelLanzador>();
        services.AddSingleton<AlmacenDeRobots>();
        services.AddHostedService<CrearSecretoDelLanzador>();

        return services;
    }
}
