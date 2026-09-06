namespace Viariato.Modules.Markets.Analysis;

public sealed record CompanyProfile(string Ticker, string Name, string Sector, string Industry);

/// <summary>One fiscal year of scraped fundamentals. Any field may be missing — public scraped
/// sources rarely give the 10-20 years of clean history Graham's original criteria assume, so the
/// analyzer degrades individual criteria to "not applicable" rather than failing them.</summary>
public sealed record FundamentalsYear(
    int FiscalYear,
    decimal? Eps,
    decimal? BookValuePerShare,
    decimal? DividendPerShare,
    decimal? CurrentAssets,
    decimal? CurrentLiabilities,
    long? SharesOutstanding);

public sealed record PricePoint(DateOnly Date, decimal Close);

public enum CriterionResult
{
    Pass,
    Fail,
    NotApplicable,
}

/// <summary>One of Graham's seven defensive-investor criteria, with the underlying number so the
/// UI can show "Ratio corriente: 2.3 (cumple)" instead of just a checkmark.</summary>
public sealed record GrahamCriterion(string Code, string Label, string? Value, CriterionResult Result);

public enum MarketSignal
{
    Bullish,
    Bearish,
    Neutral,
}

/// <summary>The full result of analyzing one company — serialized verbatim into a Trabajo's Data payload.</summary>
public sealed record CompanyAnalysisResult(
    string Ticker,
    string Name,
    string Sector,
    string Industry,
    int GrahamScore,
    int CriteriaEvaluated,
    IReadOnlyList<GrahamCriterion> Criteria,
    decimal? PeRatio,
    decimal? PbRatio,
    decimal? GrahamNumber,
    decimal? IntrinsicValue,
    decimal? MarginOfSafetyPercent,
    decimal? Sma50,
    decimal? Sma200,
    decimal? Rsi14,
    MarketSignal Signal,
    decimal PriceAtComputation,
    IReadOnlyList<FundamentalsYear> FundamentalsHistory,
    IReadOnlyList<PricePoint> RecentPrices,
    DateTimeOffset ComputedAt);
