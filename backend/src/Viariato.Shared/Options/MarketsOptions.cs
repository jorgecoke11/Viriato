namespace Viariato.Shared.Options;

public sealed class MarketsOptions
{
    public const string SectionName = "Markets";

    /// <summary>Sent on every outbound scrape request — a descriptive UA is expected etiquette for public data sources.</summary>
    public string UserAgent { get; set; } = "ViariatoMarketsBot/1.0 (+https://viariato.app)";

    /// <summary>Pause between per-company requests during a sync, to be polite to the scraped sources.</summary>
    public int SyncRequestDelayMs { get; set; } = 300;

    /// <summary>Discount rate (Y in Graham's intrinsic value formula) as a percentage, e.g. 4.4.</summary>
    public decimal DiscountRatePercent { get; set; } = 4.4m;
}
