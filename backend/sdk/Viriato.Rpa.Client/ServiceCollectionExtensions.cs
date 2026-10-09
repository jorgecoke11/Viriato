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
            sp.GetService<ILogger<RpaWorker>>()));

        return services;
    }
}
