namespace Viariato.Modules.Markets.Analysis;

/// <summary>
/// Scores a company against the seven criteria Benjamin Graham gives the "defensive investor" in
/// "The Intelligent Investor" (ch. 14), plus his classic intrinsic-value growth formula and a light
/// technical layer. Pure and side-effect-free — no I/O, no persistence — so it's trivially unit
/// testable with synthetic data regardless of where the numbers came from.
///
/// Public scraped sources rarely offer Graham's original 10-20 years of clean history, so criteria
/// that can't be evaluated with the data at hand are marked <see cref="CriterionResult.NotApplicable"/>
/// rather than failed.
/// </summary>
public static class GrahamAnalyzer
{
    private const decimal MinMarketCap = 2_000_000_000m;
    private const decimal MinCurrentRatio = 2.0m;
    private const decimal MinEarningsGrowth = 0.33m;
    private const decimal MaxPeRatio = 15m;
    private const decimal MaxCombinedMultiple = 22.5m;
    private const int MinYearsForStreakChecks = 3;
    private const int MinYearsForGrowthCheck = 4;
    private const int EarningsSmoothingYears = 3;

    // Graham's growth formula (V = EPS x (8.5 + 2g) x 4.4 / Y) was calibrated for moderate,
    // sustainable growth. Feeding it a hyper-growth stock's raw multi-hundred-percent trailing
    // growth produces valuations with no real-world meaning, so the rate it uses is capped —
    // the "earnings_growth" criterion itself still reports the true, uncapped figure.
    private const decimal MaxIntrinsicValueGrowthPercent = 20m;

    public static CompanyAnalysisResult Analyze(
        CompanyProfile company,
        IReadOnlyList<FundamentalsYear> fundamentalsAscending,
        IReadOnlyList<PricePoint> pricesAscending,
        decimal discountRatePercent,
        DateTimeOffset? now = null)
    {
        var years = fundamentalsAscending.OrderBy(y => y.FiscalYear).ToList();
        var latest = years.Count > 0 ? years[^1] : null;
        var price = pricesAscending.Count > 0 ? pricesAscending[^1].Close : 0m;
        var closes = pricesAscending.OrderBy(p => p.Date).Select(p => p.Close).ToList();

        var normalizedEps = AverageEps(years.TakeLast(EarningsSmoothingYears).ToList());
        var (growthRate, growthEvaluated) = EarningsGrowth(years);

        var criteria = new List<GrahamCriterion>
        {
            EvaluateSize(latest, price),
            EvaluateFinancialCondition(latest),
            EvaluateEarningsStability(years),
            EvaluateDividendRecord(years),
            EvaluateEarningsGrowth(growthRate, growthEvaluated),
            EvaluateModeratePe(price, normalizedEps),
            EvaluateModeratePriceToAssets(price, normalizedEps, latest),
        };

        var peRatio = RatioOrNull(price, normalizedEps);
        var pbRatio = RatioOrNull(price, latest?.BookValuePerShare);
        var grahamNumber = GrahamNumber(latest?.Eps, latest?.BookValuePerShare);

        var effectiveGrowth = growthEvaluated ? Math.Min(growthRate * 100, MaxIntrinsicValueGrowthPercent) : 0m;
        var intrinsicValue = IntrinsicValue(normalizedEps, effectiveGrowth, discountRatePercent);
        var marginOfSafety = MarginOfSafety(intrinsicValue, price);

        var sma50 = TechnicalIndicatorCalculator.SimpleMovingAverage(closes, 50);
        var sma200 = TechnicalIndicatorCalculator.SimpleMovingAverage(closes, 200);
        var rsi14 = TechnicalIndicatorCalculator.Rsi(closes, 14);
        var signal = TechnicalIndicatorCalculator.ClassifySignal(sma50, sma200);

        return new CompanyAnalysisResult(
            company.Ticker,
            company.Name,
            company.Sector,
            company.Industry,
            GrahamScore: criteria.Count(c => c.Result == CriterionResult.Pass),
            CriteriaEvaluated: criteria.Count(c => c.Result != CriterionResult.NotApplicable),
            criteria,
            peRatio,
            pbRatio,
            grahamNumber,
            intrinsicValue,
            marginOfSafety,
            sma50,
            sma200,
            rsi14,
            signal,
            price,
            years,
            pricesAscending.TakeLast(220).ToList(),
            now ?? DateTimeOffset.UtcNow);
    }

    private static GrahamCriterion EvaluateSize(FundamentalsYear? latest, decimal price)
    {
        if (latest?.SharesOutstanding is not > 0)
        {
            return new GrahamCriterion("size", "Tamaño adecuado (capitalización)", null, CriterionResult.NotApplicable);
        }

        var marketCap = price * latest.SharesOutstanding.Value;
        var passed = marketCap >= MinMarketCap;
        return new GrahamCriterion("size", "Tamaño adecuado (capitalización)", FormatMoney(marketCap),
            passed ? CriterionResult.Pass : CriterionResult.Fail);
    }

    private static GrahamCriterion EvaluateFinancialCondition(FundamentalsYear? latest)
    {
        if (latest?.CurrentAssets is not > 0 || latest.CurrentLiabilities is not > 0)
        {
            return new GrahamCriterion("financial_condition", "Ratio corriente ≥ 2", null, CriterionResult.NotApplicable);
        }

        var ratio = latest.CurrentAssets.Value / latest.CurrentLiabilities.Value;
        var passed = ratio >= MinCurrentRatio;
        return new GrahamCriterion("financial_condition", "Ratio corriente ≥ 2", $"{ratio:0.00}",
            passed ? CriterionResult.Pass : CriterionResult.Fail);
    }

    private static GrahamCriterion EvaluateEarningsStability(IReadOnlyList<FundamentalsYear> years)
    {
        var withEps = years.Where(y => y.Eps.HasValue).ToList();
        if (withEps.Count < MinYearsForStreakChecks)
        {
            return new GrahamCriterion("earnings_stability", "Beneficios positivos todos los años", null, CriterionResult.NotApplicable);
        }

        var passed = withEps.All(y => y.Eps!.Value > 0);
        return new GrahamCriterion("earnings_stability", "Beneficios positivos todos los años", $"{withEps.Count} años",
            passed ? CriterionResult.Pass : CriterionResult.Fail);
    }

    private static GrahamCriterion EvaluateDividendRecord(IReadOnlyList<FundamentalsYear> years)
    {
        var withDividend = years.Where(y => y.DividendPerShare.HasValue).ToList();
        if (withDividend.Count < MinYearsForStreakChecks)
        {
            return new GrahamCriterion("dividend_record", "Dividendo pagado todos los años", null, CriterionResult.NotApplicable);
        }

        var passed = withDividend.All(y => y.DividendPerShare!.Value > 0);
        return new GrahamCriterion("dividend_record", "Dividendo pagado todos los años", $"{withDividend.Count} años",
            passed ? CriterionResult.Pass : CriterionResult.Fail);
    }

    private static (decimal Rate, bool Evaluated) EarningsGrowth(IReadOnlyList<FundamentalsYear> years)
    {
        if (years.Count < MinYearsForGrowthCheck) return (0m, false);

        var windowSize = Math.Min(EarningsSmoothingYears, years.Count / 2);
        var firstWindow = years.Take(windowSize).ToList();
        var lastWindow = years.TakeLast(windowSize).ToList();

        var avgFirst = AverageEps(firstWindow);
        var avgLast = AverageEps(lastWindow);
        if (avgFirst is not > 0 || avgLast is null) return (0m, false);

        return ((avgLast.Value - avgFirst.Value) / avgFirst.Value, true);
    }

    private static GrahamCriterion EvaluateEarningsGrowth(decimal growthRate, bool evaluated)
    {
        if (!evaluated)
        {
            return new GrahamCriterion("earnings_growth", "Crecimiento de EPS ≥ 33%", null, CriterionResult.NotApplicable);
        }

        var passed = growthRate >= MinEarningsGrowth;
        return new GrahamCriterion("earnings_growth", "Crecimiento de EPS ≥ 33%", $"{growthRate * 100:0.0}%",
            passed ? CriterionResult.Pass : CriterionResult.Fail);
    }

    private static GrahamCriterion EvaluateModeratePe(decimal price, decimal? normalizedEps)
    {
        if (normalizedEps is not > 0)
        {
            return new GrahamCriterion("moderate_pe", "P/E moderado (≤ 15)", null, CriterionResult.NotApplicable);
        }

        var pe = price / normalizedEps.Value;
        var passed = pe <= MaxPeRatio;
        return new GrahamCriterion("moderate_pe", "P/E moderado (≤ 15)", $"{pe:0.0}",
            passed ? CriterionResult.Pass : CriterionResult.Fail);
    }

    private static GrahamCriterion EvaluateModeratePriceToAssets(decimal price, decimal? normalizedEps, FundamentalsYear? latest)
    {
        if (normalizedEps is not > 0 || latest?.BookValuePerShare is not > 0)
        {
            return new GrahamCriterion("moderate_price_to_assets", "P/E × P/B ≤ 22.5", null, CriterionResult.NotApplicable);
        }

        var pe = price / normalizedEps.Value;
        var pb = price / latest.BookValuePerShare.Value;
        var combined = pe * pb;
        var passed = combined <= MaxCombinedMultiple;
        return new GrahamCriterion("moderate_price_to_assets", "P/E × P/B ≤ 22.5", $"{combined:0.0}",
            passed ? CriterionResult.Pass : CriterionResult.Fail);
    }

    private static decimal? AverageEps(IReadOnlyList<FundamentalsYear> years)
    {
        var values = years.Where(y => y.Eps.HasValue).Select(y => y.Eps!.Value).ToList();
        return values.Count == 0 ? null : values.Average();
    }

    private static decimal? RatioOrNull(decimal numerator, decimal? denominator) =>
        denominator is > 0 ? numerator / denominator.Value : null;

    private static decimal? GrahamNumber(decimal? eps, decimal? bookValuePerShare)
    {
        if (eps is not > 0 || bookValuePerShare is not > 0) return null;
        return (decimal)Math.Sqrt((double)(22.5m * eps.Value * bookValuePerShare.Value));
    }

    /// <summary>Graham's classic growth formula: V = EPS x (8.5 + 2g) x 4.4 / Y.</summary>
    private static decimal? IntrinsicValue(decimal? normalizedEps, decimal growthRatePercent, decimal discountRatePercent)
    {
        if (normalizedEps is not > 0 || discountRatePercent <= 0) return null;
        return normalizedEps.Value * (8.5m + 2 * growthRatePercent) * 4.4m / discountRatePercent;
    }

    private static decimal? MarginOfSafety(decimal? intrinsicValue, decimal price)
    {
        if (intrinsicValue is not > 0) return null;
        return (intrinsicValue.Value - price) / intrinsicValue.Value * 100;
    }

    private static string FormatMoney(decimal value) => value switch
    {
        >= 1_000_000_000m => $"{value / 1_000_000_000m:0.0}B",
        >= 1_000_000m => $"{value / 1_000_000m:0.0}M",
        _ => $"{value:0}",
    };
}
