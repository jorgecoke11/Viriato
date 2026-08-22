using System.Linq.Expressions;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Shared;
using Viariato.Shared.Http;

namespace Viariato.Infrastructure.Crud;

public static class CrudEndpointsExtensions
{
    /// <summary>
    /// Maps GET (list + by id), POST, PATCH and DELETE for one entity. Validation runs automatically
    /// if an <see cref="IValidator{T}"/> for the request type is registered in DI; it's optional.
    /// </summary>
    public static IEndpointRouteBuilder MapCrud<TEntity, TDto, TCreateRequest, TUpdateRequest>(
        this IEndpointRouteBuilder endpoints,
        string routePrefix,
        CrudResource<TEntity, TDto, TCreateRequest, TUpdateRequest> resource)
        where TEntity : class
    {
        var group = endpoints.MapGroup(routePrefix).RequireAuthorization();

        MapListAndGet(group, resource.Id, resource.ToDto, resource.Include, resource.Search, resource.Authorize);

        group.MapPost("/", async (
            TCreateRequest request,
            AppDbContext db,
            IServiceProvider services,
            HttpContext http,
            CancellationToken ct) =>
        {
            var authResult = await RunAuthorize(resource.Authorize, http, ct);
            if (authResult is not null) return authResult;

            var validationResult = await ValidateAsync(services, request, ct);
            if (validationResult is not null) return validationResult;

            var entity = resource.Create(request);

            if (resource.BeforeCreate is not null)
            {
                var blocked = await resource.BeforeCreate(new CrudHookContext<TEntity>(http, db, entity), ct);
                if (blocked is not null) return blocked;
            }

            db.Add(entity);
            await db.SaveChangesAsync(ct);

            return Results.Ok(resource.ToDto(entity));
        });

        group.MapPatch("/{id:guid}", async (
            Guid id,
            TUpdateRequest request,
            AppDbContext db,
            IServiceProvider services,
            HttpContext http,
            CancellationToken ct) =>
        {
            var authResult = await RunAuthorize(resource.Authorize, http, ct);
            if (authResult is not null) return authResult;

            var validationResult = await ValidateAsync(services, request, ct);
            if (validationResult is not null) return validationResult;

            var entity = await FindByIdAsync(db, resource.Id, resource.Include, id, ct);
            if (entity is null) return ProblemResults.NotFound(http, "Recurso no encontrado.");

            if (resource.BeforeUpdate is not null)
            {
                var blocked = await resource.BeforeUpdate(new CrudHookContext<TEntity>(http, db, entity), request, ct);
                if (blocked is not null) return blocked;
            }

            resource.ApplyUpdate(entity, request);
            await db.SaveChangesAsync(ct);

            return Results.Ok(resource.ToDto(entity));
        });

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var authResult = await RunAuthorize(resource.Authorize, http, ct);
            if (authResult is not null) return authResult;

            var entity = await FindByIdAsync(db, resource.Id, resource.Include, id, ct);
            if (entity is null) return ProblemResults.NotFound(http, "Recurso no encontrado.");

            if (resource.BeforeDelete is not null)
            {
                var blocked = await resource.BeforeDelete(new CrudHookContext<TEntity>(http, db, entity), ct);
                if (blocked is not null) return blocked;
            }

            db.Remove(entity);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        });

        return endpoints;
    }

    /// <summary>Maps GET (list + by id) only — for resources whose source of truth isn't the database (e.g. Permissions, §5.1).</summary>
    public static IEndpointRouteBuilder MapReadOnlyCrud<TEntity, TDto>(
        this IEndpointRouteBuilder endpoints,
        string routePrefix,
        ReadOnlyCrudResource<TEntity, TDto> resource)
        where TEntity : class
    {
        var group = endpoints.MapGroup(routePrefix).RequireAuthorization();
        MapListAndGet(group, resource.Id, resource.ToDto, resource.Include, resource.Search, resource.Authorize);
        return endpoints;
    }

    private static void MapListAndGet<TEntity, TDto>(
        RouteGroupBuilder group,
        Expression<Func<TEntity, Guid>> idSelector,
        Func<TEntity, TDto> toDto,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? include,
        Func<IQueryable<TEntity>, string, IQueryable<TEntity>>? search,
        Func<HttpContext, CancellationToken, Task<IResult?>>? authorize)
        where TEntity : class
    {
        group.MapGet("/", async (
            int? page,
            int? pageSize,
            string? searchTerm,
            AppDbContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            var authResult = await RunAuthorize(authorize, http, ct);
            if (authResult is not null) return authResult;

            var currentPage = page is null or < 1 ? 1 : page.Value;
            var currentPageSize = pageSize is null or < 1 or > 100 ? 20 : pageSize.Value;

            var query = ApplyIncludes(db.Set<TEntity>().AsQueryable(), include);
            if (!string.IsNullOrWhiteSpace(searchTerm) && search is not null)
            {
                query = search(query, searchTerm.Trim());
            }

            var total = await query.CountAsync(ct);
            var items = await query
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
                .ToListAsync(ct);

            return Results.Ok(new PagedResult<TDto>(items.Select(toDto).ToList(), currentPage, currentPageSize, total));
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext db, HttpContext http, CancellationToken ct) =>
        {
            var authResult = await RunAuthorize(authorize, http, ct);
            if (authResult is not null) return authResult;

            var entity = await FindByIdAsync(db, idSelector, include, id, ct);
            return entity is null ? ProblemResults.NotFound(http, "Recurso no encontrado.") : Results.Ok(toDto(entity));
        });
    }

    private static async Task<TEntity?> FindByIdAsync<TEntity>(
        AppDbContext db,
        Expression<Func<TEntity, Guid>> idSelector,
        Func<IQueryable<TEntity>, IQueryable<TEntity>>? include,
        Guid id,
        CancellationToken ct)
        where TEntity : class
    {
        var query = ApplyIncludes(db.Set<TEntity>().AsQueryable(), include);
        return await query.FirstOrDefaultAsync(BuildIdEquals(idSelector, id), ct);
    }

    private static IQueryable<TEntity> ApplyIncludes<TEntity>(
        IQueryable<TEntity> query, Func<IQueryable<TEntity>, IQueryable<TEntity>>? include)
        where TEntity : class =>
        include is null ? query : include(query);

    private static Expression<Func<TEntity, bool>> BuildIdEquals<TEntity>(Expression<Func<TEntity, Guid>> idSelector, Guid id)
    {
        var equals = Expression.Equal(idSelector.Body, Expression.Constant(id));
        return Expression.Lambda<Func<TEntity, bool>>(equals, idSelector.Parameters[0]);
    }

    private static Task<IResult?> RunAuthorize(
        Func<HttpContext, CancellationToken, Task<IResult?>>? authorize, HttpContext http, CancellationToken ct) =>
        authorize is null ? Task.FromResult<IResult?>(null) : authorize(http, ct);

    private static async Task<IResult?> ValidateAsync<TRequest>(IServiceProvider services, TRequest request, CancellationToken ct)
    {
        var validator = services.GetService<IValidator<TRequest>>();
        if (validator is null)
        {
            return null;
        }

        var result = await validator.ValidateAsync(request, ct);
        return result.IsValid ? null : ProblemResults.ValidationProblem(result);
    }
}
