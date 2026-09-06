using Viariato.Modules.Markets.Analysis;

namespace Viariato.Modules.Markets.Tests.Analysis;

public sealed class GrahamAnalyzerTests
{
    private static readonly CompanyProfile Profile = new("TEST", "Test Corp", "Technology", "Software");

    [Fact]
    public void Analyze_WithNoFundamentalsOrPrices_MarksEveryCriterionNotApplicable()
    {
        var result = GrahamAnalyzer.Analyze(Profile, [], [], discountRatePercent: 4.4m);

        Assert.Equal(0, result.GrahamScore);
        Assert.Equal(0, result.CriteriaEvaluated);
        Assert.All(result.Criteria, c => Assert.Equal(CriterionResult.NotApplicable, c.Result));
        Assert.Null(result.PeRatio);
        Assert.Null(result.PbRatio);
        Assert.Null(result.IntrinsicValue);
    }

    [Fact]
    public void Analyze_ModelDefensiveCompany_PassesAllSevenCriteria()
    {
        // Six years of clean, growing fundamentals designed to satisfy every criterion.
        var years = new List<FundamentalsYear>
        {
            new(2018, Eps: 3.0m, BookValuePerShare: 25m, DividendPerShare: 1.0m, CurrentAssets: 500m, CurrentLiabilities: 200m, SharesOutstanding: 100_000_000),
            new(2019, Eps: 3.2m, BookValuePerShare: 28m, DividendPerShare: 1.0m, CurrentAssets: 510m, CurrentLiabilities: 205m, SharesOutstanding: 100_000_000),
            new(2020, Eps: 3.5m, BookValuePerShare: 31m, DividendPerShare: 1.1m, CurrentAssets: 520m, CurrentLiabilities: 210m, SharesOutstanding: 100_000_000),
            new(2021, Eps: 4.0m, BookValuePerShare: 34m, DividendPerShare: 1.2m, CurrentAssets: 530m, CurrentLiabilities: 215m, SharesOutstanding: 100_000_000),
            new(2022, Eps: 4.5m, BookValuePerShare: 37m, DividendPerShare: 1.3m, CurrentAssets: 540m, CurrentLiabilities: 220m, SharesOutstanding: 100_000_000),
            new(2023, Eps: 5.0m, BookValuePerShare: 40m, DividendPerShare: 1.4m, CurrentAssets: 550m, CurrentLiabilities: 225m, SharesOutstanding: 100_000_000),
        };
        var prices = new List<PricePoint> { new(new DateOnly(2024, 1, 2), 50m) };

        var result = GrahamAnalyzer.Analyze(Profile, years, prices, discountRatePercent: 4.4m);

        Assert.Equal(7, result.CriteriaEvaluated);
        Assert.Equal(7, result.GrahamScore);
        Assert.All(result.Criteria, c => Assert.Equal(CriterionResult.Pass, c.Result));

        // normalized EPS = avg(4.0, 4.5, 5.0) = 4.5 -> PE = 50 / 4.5
        Assert.Equal(11.11m, Math.Round(result.PeRatio!.Value, 2));
        // PB = 50 / 40 (latest book value per share)
        Assert.Equal(1.25m, result.PbRatio);

        Assert.NotNull(result.IntrinsicValue);
        Assert.NotNull(result.MarginOfSafetyPercent);
    }

    [Fact]
    public void Analyze_WeakCompany_FailsFinancialConditionDividendAndGrowthCriteria()
    {
        var years = new List<FundamentalsYear>
        {
            new(2020, Eps: 5.0m, BookValuePerShare: 10m, DividendPerShare: 0m, CurrentAssets: 100m, CurrentLiabilities: 150m, SharesOutstanding: 50_000_000),
            new(2021, Eps: 4.0m, BookValuePerShare: 9m, DividendPerShare: 0m, CurrentAssets: 100m, CurrentLiabilities: 160m, SharesOutstanding: 50_000_000),
            new(2022, Eps: 3.0m, BookValuePerShare: 8m, DividendPerShare: 0m, CurrentAssets: 100m, CurrentLiabilities: 170m, SharesOutstanding: 50_000_000),
            new(2023, Eps: 2.0m, BookValuePerShare: 7m, DividendPerShare: 0m, CurrentAssets: 100m, CurrentLiabilities: 180m, SharesOutstanding: 50_000_000),
        };
        var prices = new List<PricePoint> { new(new DateOnly(2024, 1, 2), 100m) };

        var result = GrahamAnalyzer.Analyze(Profile, years, prices, discountRatePercent: 4.4m);

        var byCode = result.Criteria.ToDictionary(c => c.Code);
        Assert.Equal(CriterionResult.Fail, byCode["financial_condition"].Result); // current ratio < 2
        Assert.Equal(CriterionResult.Fail, byCode["dividend_record"].Result);     // no dividend ever
        Assert.Equal(CriterionResult.Fail, byCode["earnings_growth"].Result);     // EPS shrank
        Assert.Equal(CriterionResult.Pass, byCode["earnings_stability"].Result);  // still all positive
        Assert.True(result.GrahamScore < 7);
    }

    [Fact]
    public void Analyze_NegativeEpsYear_FailsEarningsStability()
    {
        var years = new List<FundamentalsYear>
        {
            new(2021, Eps: 2.0m, BookValuePerShare: 10m, DividendPerShare: 0.5m, CurrentAssets: 300m, CurrentLiabilities: 100m, SharesOutstanding: 50_000_000),
            new(2022, Eps: -1.0m, BookValuePerShare: 9m, DividendPerShare: 0.5m, CurrentAssets: 300m, CurrentLiabilities: 100m, SharesOutstanding: 50_000_000),
            new(2023, Eps: 1.5m, BookValuePerShare: 11m, DividendPerShare: 0.5m, CurrentAssets: 300m, CurrentLiabilities: 100m, SharesOutstanding: 50_000_000),
        };
        var prices = new List<PricePoint> { new(new DateOnly(2024, 1, 2), 30m) };

        var result = GrahamAnalyzer.Analyze(Profile, years, prices, discountRatePercent: 4.4m);

        var stability = result.Criteria.Single(c => c.Code == "earnings_stability");
        Assert.Equal(CriterionResult.Fail, stability.Result);
    }

    [Fact]
    public void Analyze_SmallMarketCap_FailsSizeCriterion()
    {
        var years = new List<FundamentalsYear>
        {
            new(2023, Eps: 1.0m, BookValuePerShare: 5m, DividendPerShare: 0.1m, CurrentAssets: 10m, CurrentLiabilities: 4m, SharesOutstanding: 1_000_000),
        };
        var prices = new List<PricePoint> { new(new DateOnly(2024, 1, 2), 10m) }; // market cap = 10M, well under 2B

        var result = GrahamAnalyzer.Analyze(Profile, years, prices, discountRatePercent: 4.4m);

        var size = result.Criteria.Single(c => c.Code == "size");
        Assert.Equal(CriterionResult.Fail, size.Result);
    }

    [Fact]
    public void Analyze_GrahamNumber_MatchesFormula()
    {
        var years = new List<FundamentalsYear>
        {
            new(2023, Eps: 5m, BookValuePerShare: 40m, DividendPerShare: 1m, CurrentAssets: 100m, CurrentLiabilities: 50m, SharesOutstanding: 100_000_000),
        };
        var prices = new List<PricePoint> { new(new DateOnly(2024, 1, 2), 60m) };

        var result = GrahamAnalyzer.Analyze(Profile, years, prices, discountRatePercent: 4.4m);

        // GrahamNumber = sqrt(22.5 * EPS * BVPS) = sqrt(22.5 * 5 * 40) = sqrt(4500) ~= 67.08
        Assert.NotNull(result.GrahamNumber);
        Assert.Equal(67.08m, Math.Round(result.GrahamNumber!.Value, 2));
    }

    [Fact]
    public void Analyze_HyperGrowthCompany_CapsGrowthRateFedIntoIntrinsicValueFormula()
    {
        // EPS goes from 0.17 to 4.90 across 5 years: true growth is roughly +2700%, which would
        // blow up Graham's growth formula into a meaningless valuation if used uncapped.
        var years = new List<FundamentalsYear>
        {
            new(2022, Eps: 0.17m, BookValuePerShare: 1m, DividendPerShare: null, CurrentAssets: 100m, CurrentLiabilities: 50m, SharesOutstanding: 1_000_000_000),
            new(2023, Eps: 0.39m, BookValuePerShare: 1.1m, DividendPerShare: null, CurrentAssets: 100m, CurrentLiabilities: 50m, SharesOutstanding: 1_000_000_000),
            new(2024, Eps: 1.19m, BookValuePerShare: 1.7m, DividendPerShare: null, CurrentAssets: 100m, CurrentLiabilities: 50m, SharesOutstanding: 1_000_000_000),
            new(2025, Eps: 2.94m, BookValuePerShare: 3.2m, DividendPerShare: null, CurrentAssets: 100m, CurrentLiabilities: 50m, SharesOutstanding: 1_000_000_000),
            new(2026, Eps: 4.90m, BookValuePerShare: 6.5m, DividendPerShare: null, CurrentAssets: 100m, CurrentLiabilities: 50m, SharesOutstanding: 1_000_000_000),
        };
        var prices = new List<PricePoint> { new(new DateOnly(2026, 1, 2), 200m) };

        var result = GrahamAnalyzer.Analyze(Profile, years, prices, discountRatePercent: 4.4m);

        var growth = result.Criteria.Single(c => c.Code == "earnings_growth");
        Assert.Equal(CriterionResult.Pass, growth.Result); // the true growth is well above 33%

        // Formula ceiling with g capped at 20: normalizedEPS * (8.5 + 40) * 4.4 / 4.4 = normalizedEPS * 48.5.
        // normalizedEPS = avg(1.19, 2.94, 4.90) = 3.01 -> intrinsic value should not exceed ~146.
        Assert.NotNull(result.IntrinsicValue);
        Assert.True(result.IntrinsicValue < 200, $"Expected a capped, plausible intrinsic value, got {result.IntrinsicValue}");
    }
}
