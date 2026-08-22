using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Infrastructure.Crud;
using Viariato.Modules.Users.Auth;
using Viariato.Modules.Users.Authorization;
using Viariato.Modules.Users.Contracts;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Users.Endpoints;

internal static class RolesEndpoints
{
    public static void MapRolesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCrud("/api/v1/roles", new CrudResource<Role, RoleDto, CreateRoleRequest, UpdateRoleRequest>
        {
            Id = r => r.Id,
            Include = q => q.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission),
            ToDto = ToDto,
            Create = request => new Role
            {
                Name = request.Name,
                Description = request.Description,
                IsSystem = false,
                CreatedAt = DateTimeOffset.UtcNow,
            },
            ApplyUpdate = (role, request) =>
            {
                if (request.Name is not null) role.Name = request.Name;
                if (request.Description is not null) role.Description = request.Description;
            },
            Authorize = RevalidateRolesManageAsync,
            BeforeCreate = async (ctx, ct) =>
            {
                var nameTaken = await ctx.Db.Set<Role>().AnyAsync(r => r.Name == ctx.Entity.Name, ct);
                return nameTaken ? ProblemResults.Conflict(ctx.Http, "Ya existe un rol con ese nombre.") : null;
            },
            BeforeUpdate = async (ctx, request, ct) =>
            {
                if (request.Name is null || request.Name == ctx.Entity.Name)
                {
                    return null;
                }

                if (ctx.Entity.IsSystem)
                {
                    return ProblemResults.Conflict(ctx.Http, "No se puede renombrar un rol de sistema.");
                }

                var nameTaken = await ctx.Db.Set<Role>().AnyAsync(r => r.Id != ctx.Entity.Id && r.Name == request.Name, ct);
                return nameTaken ? ProblemResults.Conflict(ctx.Http, "Ya existe un rol con ese nombre.") : null;
            },
            BeforeDelete = async (ctx, ct) =>
            {
                if (ctx.Entity.IsSystem)
                {
                    return ProblemResults.Conflict(ctx.Http, "No se puede eliminar un rol de sistema.");
                }

                var hasUsers = await ctx.Db.Set<UserRole>().AnyAsync(ur => ur.RoleId == ctx.Entity.Id, ct);
                return hasUsers ? ProblemResults.Conflict(ctx.Http, "No se puede eliminar un rol que tiene usuarios asignados.") : null;
            },
        });

        endpoints.MapGroup("/api/v1/roles")
            .RequireAuthorization()
            .MapPut("/{id:guid}/permissions", UpdateRolePermissionsAsync);

        // Permissions are declared in code and synced to the database on startup (§5.1) — the
        // database is never their source of truth, so this stays read-only.
        endpoints.MapReadOnlyCrud("/api/v1/permissions", new ReadOnlyCrudResource<Permission, PermissionDto>
        {
            Id = p => p.Id,
            ToDto = p => new PermissionDto(p.Id, p.Name, p.Module, p.Action, p.Description),
            Search = (query, term) => query.Where(p => p.Name.ToLower().Contains(term.ToLower())),
            Authorize = RevalidateRolesManageAsync,
        });
    }

    private static async Task<IResult?> RevalidateRolesManageAsync(HttpContext http, CancellationToken ct)
    {
        var checker = http.RequestServices.GetRequiredService<IPermissionChecker>();
        var userId = http.User.GetUserId();

        var allowed = await checker.HasPermissionAsync(userId, Permissions.RolesManage, ct);
        return allowed ? null : ProblemResults.Forbidden(http, "No tienes permiso para gestionar roles.");
    }

    private static RoleDto ToDto(Role role) => new(
        role.Id,
        role.Name,
        role.Description,
        role.IsSystem,
        role.RolePermissions.Select(rp => rp.Permission.Name).ToList(),
        role.CreatedAt);

    private static async Task<IResult> UpdateRolePermissionsAsync(
        Guid id,
        UpdateRolePermissionsRequest request,
        AppDbContext db,
        HttpContext http,
        CancellationToken ct)
    {
        var authResult = await RevalidateRolesManageAsync(http, ct);
        if (authResult is not null)
        {
            return authResult;
        }

        var role = await db.Set<Role>()
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role is null)
        {
            return ProblemResults.NotFound(http, "Rol no encontrado.");
        }

        var validPermissionIds = await db.Set<Permission>()
            .Where(p => request.PermissionIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(ct);

        var currentIds = role.RolePermissions.Select(rp => rp.PermissionId).ToHashSet();

        var toRemove = role.RolePermissions.Where(rp => !validPermissionIds.Contains(rp.PermissionId)).ToList();
        db.RemoveRange(toRemove);

        foreach (var permissionId in validPermissionIds.Where(pid => !currentIds.Contains(pid)))
        {
            db.Add(new RolePermission { RoleId = id, PermissionId = permissionId });
        }

        await db.SaveChangesAsync(ct);

        var refreshed = await db.Set<Role>()
            .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstAsync(r => r.Id == id, ct);

        return Results.Ok(ToDto(refreshed));
    }
}
