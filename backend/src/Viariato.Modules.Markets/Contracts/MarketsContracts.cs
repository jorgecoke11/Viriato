using Viariato.Modules.Markets.Analysis;

namespace Viariato.Modules.Markets.Contracts;

public sealed record CompanyListItemDto(
    string Ticker,
    string Name,
    string Sector,
    int GrahamScore,
    int CriteriaEvaluated,
    decimal? PeRatio,
    decimal? PbRatio,
    decimal? MarginOfSafetyPercent,
    string Signal,
    DateTimeOffset ComputedAt);

public sealed record CompanyDetailDto(Guid TrabajoId, CompanyAnalysisResult Analysis);

public sealed record TriggerSyncRequest(string Scope);

public sealed record TriggerSyncResponse(Guid TrabajoId);
