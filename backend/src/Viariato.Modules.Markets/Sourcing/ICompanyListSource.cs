namespace Viariato.Modules.Markets.Sourcing;

public enum MarketIndex
{
    Sp500,
    Ndx100,
}

public sealed record CompanyListItem(string Ticker, string Name, string Sector, string Industry);

public interface ICompanyListSource
{
    Task<IReadOnlyList<CompanyListItem>> GetCompaniesAsync(MarketIndex index, CancellationToken ct);
}
