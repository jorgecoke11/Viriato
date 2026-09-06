using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Viariato.Modules.Markets.Sourcing;
using Viariato.Modules.Markets.Sync;
using Viariato.Shared.Options;

namespace Viariato.Modules.Markets;

public static class DependencyInjection
{
    public static IServiceCollection AddMarketsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<ICompanyListSource, WikipediaCompanyListSource>(ConfigureScrapingClient);
        services.AddHttpClient<IPriceHistorySource, YahooFinancePriceHistorySource>(ConfigureScrapingClient);
        services.AddHttpClient<IFundamentalsSource, StockAnalysisFundamentalsSource>(ConfigureScrapingClient);

        services.AddScoped<IMarketSyncOrchestrator, MarketSyncOrchestrator>();

        return services;
    }

    private static void ConfigureScrapingClient(IServiceProvider services, HttpClient client)
    {
        var options = services.GetRequiredService<IOptions<MarketsOptions>>().Value;
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
    }
}
