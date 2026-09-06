using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace Viariato.Modules.Markets.Sourcing;

/// <summary>
/// Parses the constituent tables Wikipedia maintains for S&amp;P 500 and Nasdaq-100 — the most
/// stable freely-scrapable source for index membership. Reads columns by header name rather than
/// fixed position, since Wikipedia editors reorder columns occasionally; if a page's markup changes
/// shape entirely this throws, which surfaces as a failed Trabajo rather than silently wrong data.
/// </summary>
public sealed partial class WikipediaCompanyListSource(HttpClient httpClient) : ICompanyListSource
{
    private const string Sp500Url = "https://en.wikipedia.org/wiki/List_of_S%26P_500_companies";
    private const string Ndx100Url = "https://en.wikipedia.org/wiki/List_of_NASDAQ-100_companies";

    public async Task<IReadOnlyList<CompanyListItem>> GetCompaniesAsync(MarketIndex index, CancellationToken ct)
    {
        var url = index == MarketIndex.Sp500 ? Sp500Url : Ndx100Url;
        var html = await httpClient.GetStringAsync(url, ct);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var table = doc.DocumentNode
            .SelectNodes("//table[contains(@class,'wikitable')]")
            ?.FirstOrDefault(HasTickerColumn)
            ?? throw new InvalidOperationException($"No se encontró la tabla de constituyentes en {url}.");

        var headerCells = SelectNodesOrEmpty(table, ".//tr[1]/th", ".//tr[1]/td");
        var headers = headerCells.Select(h => CleanText(h.InnerText)).ToList();

        var tickerCol = FindColumn(headers, "Symbol", "Ticker")
            ?? throw new InvalidOperationException($"No se encontró la columna de ticker en la tabla de {url}.");
        var nameCol = FindColumn(headers, "Security", "Company")
            ?? throw new InvalidOperationException($"No se encontró la columna de nombre en la tabla de {url}.");
        var sectorCol = FindColumn(headers, "GICS Sector", "ICB Industry", "Sector");
        var industryCol = FindColumn(headers, "GICS Sub-Industry", "ICB Subsector", "Sub-Industry");

        var rows = SelectNodesOrEmpty(table, ".//tr[position() > 1]");
        var results = new List<CompanyListItem>();

        foreach (var row in rows)
        {
            var cells = row.SelectNodes("./td");
            if (cells is null || tickerCol >= cells.Count || nameCol >= cells.Count) continue;

            var ticker = CleanText(cells[tickerCol].InnerText);
            var name = CleanText(cells[nameCol].InnerText);
            if (string.IsNullOrWhiteSpace(ticker) || string.IsNullOrWhiteSpace(name)) continue;

            results.Add(new CompanyListItem(
                ticker,
                name,
                sectorCol is int sc && sc < cells.Count ? CleanText(cells[sc].InnerText) : "",
                industryCol is int ic && ic < cells.Count ? CleanText(cells[ic].InnerText) : ""));
        }

        return results;
    }

    private static bool HasTickerColumn(HtmlNode table)
    {
        var headerCells = table.SelectNodes(".//tr[1]/th") ?? table.SelectNodes(".//tr[1]/td");
        if (headerCells is null) return false;

        return headerCells.Any(h =>
        {
            var text = CleanText(h.InnerText);
            return text.Equals("Symbol", StringComparison.OrdinalIgnoreCase) ||
                   text.Equals("Ticker", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static List<HtmlNode> SelectNodesOrEmpty(HtmlNode node, params string[] xpaths)
    {
        foreach (var xpath in xpaths)
        {
            var found = node.SelectNodes(xpath);
            if (found is { Count: > 0 }) return [.. found];
        }

        return [];
    }

    private static int? FindColumn(IReadOnlyList<string> headers, params string[] candidates)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            if (candidates.Any(c => headers[i].Equals(c, StringComparison.OrdinalIgnoreCase)))
            {
                return i;
            }
        }

        return null;
    }

    private static string CleanText(string raw) =>
        FootnoteRegex().Replace(HtmlEntity.DeEntitize(raw) ?? raw, "").Trim();

    [GeneratedRegex(@"\[\d+\]")]
    private static partial Regex FootnoteRegex();
}
