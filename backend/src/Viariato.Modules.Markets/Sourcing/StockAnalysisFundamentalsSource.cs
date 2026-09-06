using System.Globalization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Viariato.Modules.Markets.Analysis;

namespace Viariato.Modules.Markets.Sourcing;

/// <summary>
/// Scrapes per-year fundamentals from stockanalysis.com's free financials pages. This is the most
/// fragile source in the pipeline — it depends on a third party's HTML layout and isn't a published
/// data feed like Wikipedia's tables or Stooq's CSV export, so its ToS/stability is a real risk.
/// Isolated behind <see cref="IFundamentalsSource"/> precisely so it can be swapped for a paid API
/// later without touching <see cref="GrahamAnalyzer"/> or anything downstream.
/// </summary>
public sealed partial class StockAnalysisFundamentalsSource(HttpClient httpClient) : IFundamentalsSource
{
    public async Task<IReadOnlyList<FundamentalsYear>> GetFundamentalsAsync(string ticker, CancellationToken ct)
    {
        var symbol = ticker.Trim().ToLowerInvariant().Replace(".", "-");

        var incomeDoc = await LoadAsync($"https://stockanalysis.com/stocks/{symbol}/financials/", ct);
        var balanceDoc = await LoadAsync($"https://stockanalysis.com/stocks/{symbol}/financials/balance-sheet/", ct);

        var years = ExtractFiscalYears(incomeDoc);
        if (years.Count == 0)
        {
            throw new InvalidOperationException($"No se encontraron años fiscales para '{ticker}' en stockanalysis.com.");
        }

        var eps = FindRow(incomeDoc, "Earnings Per Share");
        var dividend = FindRow(incomeDoc, "Dividend Per Share");
        var currentAssets = FindRow(balanceDoc, "Total Current Assets");
        var currentLiabilities = FindRow(balanceDoc, "Total Current Liabilities");
        var bookValue = FindRow(balanceDoc, "Book Value Per Share");
        var sharesOutstanding = FindRow(balanceDoc, "Total Common Shares Outstanding");

        var result = new List<FundamentalsYear>();
        foreach (var (fiscalYear, columnIndex) in years)
        {
            result.Add(new FundamentalsYear(
                fiscalYear,
                Eps: ValueAt(eps, columnIndex),
                BookValuePerShare: ValueAt(bookValue, columnIndex),
                DividendPerShare: ValueAt(dividend, columnIndex),
                CurrentAssets: ValueAt(currentAssets, columnIndex),
                CurrentLiabilities: ValueAt(currentLiabilities, columnIndex),
                SharesOutstanding: ValueAt(sharesOutstanding, columnIndex) is decimal shares
                    ? (long)(shares * 1_000_000m)
                    : null));
        }

        return result.OrderBy(y => y.FiscalYear).ToList();
    }

    private async Task<HtmlDocument> LoadAsync(string url, CancellationToken ct)
    {
        var html = await httpClient.GetStringAsync(url, ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    /// <summary>Maps fiscal year -> column index using the first table's first header row (skips "Fiscal Year" and "TTM").</summary>
    private static List<(int FiscalYear, int ColumnIndex)> ExtractFiscalYears(HtmlDocument doc)
    {
        var headerRow = doc.DocumentNode.SelectSingleNode("(//table)[1]//thead/tr[1]");
        if (headerRow is null) return [];

        var cells = ((IEnumerable<HtmlNode>?)headerRow.SelectNodes("./th|./td") ?? []).ToList();
        var years = new List<(int, int)>();

        for (var i = 0; i < cells.Count; i++)
        {
            var match = FiscalYearRegex().Match(cells[i].InnerText);
            if (match.Success)
            {
                years.Add((int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), i));
            }
        }

        return years;
    }

    /// <summary>Finds the first table (out of every table on the page) whose row label starts with <paramref name="label"/>.</summary>
    private static HtmlNodeCollection? FindRow(HtmlDocument doc, string label)
    {
        var tables = doc.DocumentNode.SelectNodes("//table");
        if (tables is null) return null;

        foreach (var table in tables)
        {
            var rows = table.SelectNodes(".//tbody/tr");
            if (rows is null) continue;

            foreach (var row in rows)
            {
                var firstCell = row.SelectSingleNode("./th|./td");
                if (firstCell is not null && firstCell.InnerText.Trim().StartsWith(label, StringComparison.OrdinalIgnoreCase))
                {
                    return row.SelectNodes("./th|./td");
                }
            }
        }

        return null;
    }

    private static decimal? ValueAt(HtmlNodeCollection? row, int columnIndex)
    {
        if (row is null || columnIndex >= row.Count) return null;

        var text = row[columnIndex].InnerText.Trim();
        if (string.IsNullOrEmpty(text) || text == "-") return null;

        var negative = text.StartsWith('(') && text.EndsWith(')');
        var cleaned = text.Trim('(', ')').Replace(",", "").Replace("$", "").Replace("%", "");

        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? (negative ? -value : value)
            : null;
    }

    [GeneratedRegex(@"FY\s*(\d{4})")]
    private static partial Regex FiscalYearRegex();
}
