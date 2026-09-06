using System.Text.Json;
using Viariato.Modules.Markets.Analysis;

namespace Viariato.Modules.Markets.Sourcing;

/// <summary>
/// Reads daily closes from Yahoo Finance's public chart JSON endpoint — an unofficial but widely
/// used, plain-HTTP data feed (no login wall, no JS challenge). Chosen over Stooq's CSV export,
/// which started gating downloads behind a JavaScript proof-of-work bot check; that kind of gate is
/// not something to script around, so this source was swapped in behind the same
/// <see cref="IPriceHistorySource"/> interface instead — nothing downstream had to change.
/// </summary>
public sealed class YahooFinancePriceHistorySource(HttpClient httpClient) : IPriceHistorySource
{
    public async Task<IReadOnlyList<PricePoint>> GetDailyClosesAsync(string ticker, CancellationToken ct)
    {
        var symbol = Uri.EscapeDataString(ticker.Trim().ToUpperInvariant());
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?range=2y&interval=1d";

        using var response = await httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Yahoo Finance no devolvió datos de precio para '{ticker}' ({(int)response.StatusCode}).");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var root = doc.RootElement.GetProperty("chart");
        if (root.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
        {
            throw new InvalidOperationException($"Yahoo Finance no encontró el ticker '{ticker}'.");
        }

        var results = root.GetProperty("result");
        if (results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"Yahoo Finance no devolvió datos de precio para '{ticker}'.");
        }

        var result = results[0];
        var timestamps = result.GetProperty("timestamp");
        var closes = result.GetProperty("indicators").GetProperty("quote")[0].GetProperty("close");

        var points = new List<PricePoint>(timestamps.GetArrayLength());
        for (var i = 0; i < timestamps.GetArrayLength(); i++)
        {
            if (closes[i].ValueKind != JsonValueKind.Number) continue;

            var date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64()).UtcDateTime);
            points.Add(new PricePoint(date, closes[i].GetDecimal()));
        }

        return points;
    }
}
