using Viariato.Modules.Markets.Sourcing;

namespace Viariato.Modules.Markets.Tests.Sourcing;

public sealed class StockAnalysisFundamentalsSourceTests
{
    private const string IncomeStatementHtml = """
        <html><body>
        <table>
        <thead><tr><th>Fiscal Year</th><th>TTM</th><th>FY 2023</th><th>FY 2022</th></tr></thead>
        <tbody>
        <tr><td>Earnings Per Share    EPS Growth</td><td>6.00</td><td>6.13</td><td>6.11</td></tr>
        <tr><td>Dividend Per Share    Dividend Per Share Growth</td><td>0.98</td><td>0.94</td><td>0.90</td></tr>
        </tbody>
        </table>
        </body></html>
        """;

    private const string BalanceSheetHtml = """
        <html><body>
        <table>
        <thead><tr><th>Fiscal Year</th><th>TTM</th><th>FY 2023</th><th>FY 2022</th></tr></thead>
        <tbody>
        <tr><td>Total Current Assets</td><td>150,000</td><td>143,566</td><td>135,405</td></tr>
        </tbody>
        </table>
        <table>
        <tbody>
        <tr><td>Total Current Liabilities</td><td>140,000</td><td>145,308</td><td>153,982</td></tr>
        </tbody>
        </table>
        <table>
        <tbody>
        <tr><td>Total Common Shares Outstanding</td><td>15,600</td><td>15,550</td><td>15,943</td></tr>
        <tr><td>Book Value Per Share</td><td>3.90</td><td>4.00</td><td>3.18</td></tr>
        </tbody>
        </table>
        </body></html>
        """;

    private static HttpClient CreateClient() => RoutedHttpMessageHandler.CreateClient(new Dictionary<string, string>
    {
        ["/financials/balance-sheet/"] = BalanceSheetHtml,
        ["/financials/"] = IncomeStatementHtml,
    });

    [Fact]
    public async Task GetFundamentalsAsync_CombinesIncomeAndBalanceSheetPagesByFiscalYear()
    {
        var source = new StockAnalysisFundamentalsSource(CreateClient());

        var years = await source.GetFundamentalsAsync("AAPL", CancellationToken.None);

        Assert.Equal(2, years.Count);
        Assert.Equal(2022, years[0].FiscalYear);
        Assert.Equal(2023, years[1].FiscalYear);

        var fy2023 = years[1];
        Assert.Equal(6.13m, fy2023.Eps);
        Assert.Equal(4.00m, fy2023.BookValuePerShare);
        Assert.Equal(0.94m, fy2023.DividendPerShare);
        Assert.Equal(143566m, fy2023.CurrentAssets);
        Assert.Equal(145308m, fy2023.CurrentLiabilities);
        Assert.Equal(15_550_000_000L, fy2023.SharesOutstanding);
    }

    [Fact]
    public async Task GetFundamentalsAsync_IgnoresTtmColumn()
    {
        var source = new StockAnalysisFundamentalsSource(CreateClient());

        var years = await source.GetFundamentalsAsync("AAPL", CancellationToken.None);

        Assert.DoesNotContain(years, y => y.FiscalYear == 0);
    }
}
