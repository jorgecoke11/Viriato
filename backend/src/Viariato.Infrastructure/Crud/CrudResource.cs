using System.Linq.Expressions;
using Microsoft.AspNetCore.Http;

namespace Viariato.Infrastructure.Crud;

/// <summary>Everything a request handler needs to run a business-rule hook for one entity.</summary>
public sealed record CrudHookContext<TEntity>(HttpContext Http, AppDbContext Db, TEntity Entity)
    where TEntity : class;

/// <summary>
/// Describes how a single EF entity maps to its DTO and request contracts, so
/// <see cref="CrudEndpointsExtensions.MapCrud{TEntity,TDto,TCreateRequest,TUpdateRequest}"/> can generate
/// GET (list + by id), POST, PATCH and DELETE endpoints for it without repeating that boilerplate per module.
/// </summary>
public sealed class CrudResource<TEntity, TDto, TCreateRequest, TUpdateRequest>
    where TEntity : class
{
    /// Selects the primary key, e.g. <c>r => r.Id</c>. An expression (not a delegate) so it can be
    /// translated into a SQL WHERE clause instead of loading the whole table to filter in memory.
    public required Expression<Func<TEntity, Guid>> Id { get; init; }

    public required Func<TEntity, TDto> ToDto { get; init; }
    public required Func<TCreateRequest, TEntity> Create { get; init; }
    public required Action<TEntity, TUpdateRequest> ApplyUpdate { get; init; }

    /// Eager-loads related data (e.g. <c>q => q.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)</c>)
    /// needed by <see cref="ToDto"/>. Applied to every query this resource runs.
    public Func<IQueryable<TEntity>, IQueryable<TEntity>>? Include { get; init; }

    /// Case-insensitive search filter applied when the caller passes ?search=. Omit to disable search.
    public Func<IQueryable<TEntity>, string, IQueryable<TEntity>>? Search { get; init; }

    /// <summary>
    /// Runs before every request this resource maps (list, get, create, update, delete). Return a
    /// result to short-circuit the request, or null to continue. Use this to revalidate a sensitive
    /// permission against the database instead of trusting the JWT claim alone (see §5.3).
    /// </summary>
    public Func<HttpContext, CancellationToken, Task<IResult?>>? Authorize { get; init; }

    public Func<CrudHookContext<TEntity>, CancellationToken, Task<IResult?>>? BeforeCreate { get; init; }
    public Func<CrudHookContext<TEntity>, TUpdateRequest, CancellationToken, Task<IResult?>>? BeforeUpdate { get; init; }
    public Func<CrudHookContext<TEntity>, CancellationToken, Task<IResult?>>? BeforeDelete { get; init; }
}

/// <summary>The read-only counterpart of <see cref="CrudResource{TEntity,TDto,TCreateRequest,TUpdateRequest}"/>.</summary>
public sealed class ReadOnlyCrudResource<TEntity, TDto>
    where TEntity : class
{
    public required Expression<Func<TEntity, Guid>> Id { get; init; }
    public required Func<TEntity, TDto> ToDto { get; init; }
    public Func<IQueryable<TEntity>, IQueryable<TEntity>>? Include { get; init; }
    public Func<IQueryable<TEntity>, string, IQueryable<TEntity>>? Search { get; init; }
    public Func<HttpContext, CancellationToken, Task<IResult?>>? Authorize { get; init; }
}
