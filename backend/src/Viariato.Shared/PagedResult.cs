namespace Viariato.Shared;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
