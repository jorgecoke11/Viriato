using Viariato.Modules.Markets.Analysis;

namespace Viariato.Modules.Markets.Tests.Analysis;

public sealed class TechnicalIndicatorCalculatorTests
{
    [Fact]
    public void SimpleMovingAverage_ReturnsNull_WhenNotEnoughData()
    {
        var closes = Enumerable.Range(1, 10).Select(i => (decimal)i).ToList();
        Assert.Null(TechnicalIndicatorCalculator.SimpleMovingAverage(closes, 20));
    }

    [Fact]
    public void SimpleMovingAverage_AveragesLastPeriodValues()
    {
        // Last 3 of [1..5] are 3,4,5 -> average 4.
        var closes = new List<decimal> { 1, 2, 3, 4, 5 };
        Assert.Equal(4m, TechnicalIndicatorCalculator.SimpleMovingAverage(closes, 3));
    }

    [Fact]
    public void Rsi_ReturnsNull_WhenNotEnoughData()
    {
        var closes = Enumerable.Range(1, 10).Select(i => (decimal)i).ToList();
        Assert.Null(TechnicalIndicatorCalculator.Rsi(closes, 14));
    }

    [Fact]
    public void Rsi_Returns100_WhenNoLosses()
    {
        var closes = Enumerable.Range(1, 15).Select(i => (decimal)i).ToList(); // strictly increasing
        Assert.Equal(100m, TechnicalIndicatorCalculator.Rsi(closes, 14));
    }

    [Fact]
    public void Rsi_ReturnsLowValue_WhenStrictlyDecreasing()
    {
        var closes = Enumerable.Range(1, 15).Select(i => (decimal)(15 - i)).ToList(); // strictly decreasing
        var rsi = TechnicalIndicatorCalculator.Rsi(closes, 14);
        Assert.NotNull(rsi);
        Assert.True(rsi < 5, $"Expected RSI near 0 for an all-losses series, got {rsi}");
    }

    [Theory]
    [InlineData(110, 100, MarketSignal.Bullish)]
    [InlineData(90, 100, MarketSignal.Bearish)]
    [InlineData(100, 100, MarketSignal.Neutral)]
    public void ClassifySignal_ComparesSma50ToSma200(decimal sma50, decimal sma200, MarketSignal expected)
    {
        Assert.Equal(expected, TechnicalIndicatorCalculator.ClassifySignal(sma50, sma200));
    }

    [Fact]
    public void ClassifySignal_IsNeutral_WhenEitherAverageMissing()
    {
        Assert.Equal(MarketSignal.Neutral, TechnicalIndicatorCalculator.ClassifySignal(null, 100));
        Assert.Equal(MarketSignal.Neutral, TechnicalIndicatorCalculator.ClassifySignal(100, null));
    }
}
