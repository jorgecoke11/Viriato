namespace Viariato.Modules.Markets.Analysis;

/// <summary>Pure, side-effect-free indicator math over an ascending series of closing prices.</summary>
public static class TechnicalIndicatorCalculator
{
    public static decimal? SimpleMovingAverage(IReadOnlyList<decimal> closesAscending, int period)
    {
        if (closesAscending.Count < period) return null;
        return closesAscending.TakeLast(period).Average();
    }

    /// <summary>Wilder's RSI over the last <paramref name="period"/> changes (needs period + 1 closes).</summary>
    public static decimal? Rsi(IReadOnlyList<decimal> closesAscending, int period = 14)
    {
        if (closesAscending.Count < period + 1) return null;

        var window = closesAscending.TakeLast(period + 1).ToArray();
        decimal gainSum = 0, lossSum = 0;

        for (var i = 1; i < window.Length; i++)
        {
            var change = window[i] - window[i - 1];
            if (change > 0) gainSum += change;
            else lossSum -= change;
        }

        var avgGain = gainSum / period;
        var avgLoss = lossSum / period;
        if (avgLoss == 0) return 100m;

        var relativeStrength = avgGain / avgLoss;
        return 100m - 100m / (1 + relativeStrength);
    }

    public static MarketSignal ClassifySignal(decimal? sma50, decimal? sma200)
    {
        if (sma50 is null || sma200 is null) return MarketSignal.Neutral;
        if (sma50 > sma200) return MarketSignal.Bullish;
        if (sma50 < sma200) return MarketSignal.Bearish;
        return MarketSignal.Neutral;
    }
}
