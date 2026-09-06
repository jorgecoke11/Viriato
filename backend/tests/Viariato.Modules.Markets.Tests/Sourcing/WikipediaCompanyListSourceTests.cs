using Viariato.Modules.Markets.Sourcing;

namespace Viariato.Modules.Markets.Tests.Sourcing;

public sealed class WikipediaCompanyListSourceTests
{
    private const string SampleHtml = """
        <html><body>
        <table class="wikitable sortable">
        <tr><th>Symbol</th><th>Security</th><th>GICS Sector</th><th>GICS Sub-Industry</th></tr>
        <tr><td>AAPL</td><td>Apple Inc.</td><td>Information Technology</td><td>Technology Hardware, Storage &amp; Peripherals</td></tr>
        <tr><td>MSFT</td><td>Microsoft Corp.[1]</td><td>Information Technology</td><td>Systems Software</td></tr>
        </table>
        </body></html>
        """;

    [Fact]
    public async Task GetCompaniesAsync_ParsesRowsByHeaderName()
    {
        var client = StubHttpMessageHandler.CreateClient(SampleHtml);
        var source = new WikipediaCompanyListSource(client);

        var companies = await source.GetCompaniesAsync(MarketIndex.Sp500, CancellationToken.None);

        Assert.Equal(2, companies.Count);

        var apple = companies.Single(c => c.Ticker == "AAPL");
        Assert.Equal("Apple Inc.", apple.Name);
        Assert.Equal("Information Technology", apple.Sector);
        Assert.Equal("Technology Hardware, Storage & Peripherals", apple.Industry);
    }

    [Fact]
    public async Task GetCompaniesAsync_StripsWikipediaFootnoteMarkers()
    {
        var client = StubHttpMessageHandler.CreateClient(SampleHtml);
        var source = new WikipediaCompanyListSource(client);

        var companies = await source.GetCompaniesAsync(MarketIndex.Sp500, CancellationToken.None);

        var msft = companies.Single(c => c.Ticker == "MSFT");
        Assert.Equal("Microsoft Corp.", msft.Name);
    }

    [Fact]
    public async Task GetCompaniesAsync_Throws_WhenNoTickerColumnFound()
    {
        const string html = "<html><body><table class=\"wikitable\"><tr><th>Foo</th><th>Bar</th></tr></table></body></html>";
        var client = StubHttpMessageHandler.CreateClient(html);
        var source = new WikipediaCompanyListSource(client);

        await Assert.ThrowsAsync<InvalidOperationException>(() => source.GetCompaniesAsync(MarketIndex.Ndx100, CancellationToken.None));
    }
}
