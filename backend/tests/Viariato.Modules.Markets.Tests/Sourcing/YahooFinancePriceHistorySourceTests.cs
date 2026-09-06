using Viariato.Modules.Markets.Sourcing;

namespace Viariato.Modules.Markets.Tests.Sourcing;

public sealed class YahooFinancePriceHistorySourceTests
{
    // 2024-01-02T00:00:00Z and 2024-01-03T00:00:00Z, with a null close in between to simulate a gap.
    private const string SampleJson = """
        {
          "chart": {
            "result": [
              {
                "timestamp": [1704153600, 1704240000, 1704326400],
                "indicators": {
                  "quote": [
                    { "close": [185.64, null, 186.19] }
                  ]
                }
              }
            ],
            "error": null
          }
        }
        """;

    [Fact]
    public async Task GetDailyClosesAsync_ParsesTimestampsAndSkipsNullCloses()
    {
        var client = StubHttpMessageHandler.CreateClient(SampleJson, "application/json");
        var source = new YahooFinancePriceHistorySource(client);

        var points = await source.GetDailyClosesAsync("AAPL", CancellationToken.None);

        Assert.Equal(2, points.Count);
        Assert.Equal(new DateOnly(2024, 1, 2), points[0].Date);
        Assert.Equal(185.64m, points[0].Close);
        Assert.Equal(new DateOnly(2024, 1, 4), points[1].Date);
        Assert.Equal(186.19m, points[1].Close);
    }

    [Fact]
    public async Task GetDailyClosesAsync_Throws_WhenChartHasError()
    {
        const string json = """{"chart":{"result":null,"error":{"code":"Not Found","description":"No data found"}}}""";
        var client = StubHttpMessageHandler.CreateClient(json, "application/json");
        var source = new YahooFinancePriceHistorySource(client);

        await Assert.ThrowsAsync<InvalidOperationException>(() => source.GetDailyClosesAsync("NOPE", CancellationToken.None));
    }
}
