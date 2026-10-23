using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Viriato.Rpa.Client;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IRpaClient"/> (on <c>IHttpClientFactory</c>) and <see cref="RpaWorker"/>.
    /// <code>services.AddViriatoRpaClient(o => { o.BaseUrl = "https://viriato.tuempresa.com"; o.ApiKey = "..."; });</code>
    /// </summary>
    public static IServiceCollection AddViriatoRpaClient(
        this IServiceCollection services,
        Action<RpaClientOptions> configure,
        Action<RpaWorkerOptions>? configureWorker = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        if (configureWorker is not null)
        {
            services.Configure(configureWorker);
        }

        services.AddHttpClient<IRpaClient, RpaClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<RpaClientOptions>>().Value;
            http.BaseAddress = options.ResolveBaseAddress();
            http.Timeout = options.Timeout;
            http.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
            http.DefaultRequestHeaders.Add(Viariato.ApiContracts.RpaHeaders.Instancia, options.ResolveInstanciaId());
        });

        services.AddTransient(sp => new RpaWorker(
            sp.GetRequiredService<IRpaClient>(),
            sp.GetService<IOptions<RpaWorkerOptions>>()?.Value,
            sp.GetService<ILogger<RpaWorker>>(),
            sp.GetServices<IAjusteDelDespliegue>()));

        return services;
    }

    /// <summary>
    /// Serves the robot's screen live (x11vnc + noVNC) as Viriato decides for its Despliegue — off, low, medium or high — and follows
    /// changes made there while the robot runs. <paramref name="configure"/> sets what belongs to the machine (ports, password, a veto).
    /// </summary>
    public static IServiceCollection AddViriatoVistaEnDirecto(this IServiceCollection services, Action<VistaEnDirectoOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (configure is not null) services.Configure(configure);
        else services.AddOptions<VistaEnDirectoOptions>();

        services.AddSingleton<ILanzadorDeProcesos, LanzadorDeProcesos>();
        services.AddSingleton<VistaEnDirectoNoVnc>();
        services.AddSingleton<IAjusteDelDespliegue>(sp => sp.GetRequiredService<VistaEnDirectoNoVnc>());
        return services;
    }
}
