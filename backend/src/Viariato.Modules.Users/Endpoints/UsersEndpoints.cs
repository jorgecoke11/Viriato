using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Users.Auth;
using Viariato.Modules.Users.Authorization;
using Viariato.Modules.Users.Contracts;
using Viariato.Modules.Users.Domain;
using Viariato.Modules.Users.Validation;
using Viariato.Shared;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Users.Endpoints;

internal static class UsersEndpoints
{
    public static void MapUsersManagementEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/users").RequireAuthorization();

        group.MapGet("/me", GetMeAsync);
        group.MapPatch("/me", UpdateMeAsync);
        group.MapPost("/me/password", ChangePasswordAsync);

        group.MapGet("/", ListUsersAsync).RequireAuthorization(Permissions.UsersRead);
        group.MapGet("/{id:guid}", GetByIdAsync).RequireAuthorization(Permissions.UsersRead);

        group.MapPost("/", CreateUserAsync).RequireAuthorization(Permissions.UsersManage);
        group.MapPatch("/{id:guid}", UpdateUserAsync).RequireAuthorization(Permissions.UsersManage);
        group.MapDelete("/{id:guid}", DeactivateUserAsync).RequireAuthorization(Permissions.UsersManage);

        group.MapPost("/{id:guid}/roles", AssignRoleAsync).RequireAuthorization(Permissions.UsersManage);
        group.MapDelete("/{id:guid}/roles/{roleId:guid}", RemoveRoleAsync).RequireAuthorization(Permissions.UsersManage);
    }

    private static async Task<IResult> GetMeAsync(ClaimsPrincipal principal, AppDbContext db, TokenService tokens, HttpContext http, CancellationToken ct)
    {
        var userId = principal.GetUserId();
        var user = await db.Set<User>().FindAsync([userId], ct);
        if (user is null)
        {
            return ProblemResults.NotFound(http, "Usuario no encontrado.");
        }

        var (roles, permissions) = await tokens.GetRolesAndPermissionsAsync(userId, ct);
        return Results.Ok(user.ToDto(roles, permissions));
    }

    private static async Task<IResult> UpdateMeAsync(
        UpdateProfileRequest request,
        UpdateProfileRequestValidator validator,
        ClaimsPrincipal principal,
        AppDbContext db,
        TokenService tokens,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            return ProblemResults.ValidationProblem(validation);
        }

        var userId = principal.GetUserId();
        var user = await db.Set<User>().FindAsync([userId], ct);
        if (user is null)
        {
            return ProblemResults.NotFound(http, "Usuario no encontrado.");
        }

        if (request.DisplayName is not null) user.DisplayName = request.DisplayName;
        if (request.BaseCurrency is not null) user.BaseCurrency = request.BaseCurrency;
        if (request.TimeZone is not null) user.TimeZone = request.TimeZone;
        if (request.Locale is not null) user.Locale = request.Locale;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        var (roles, permissions) = await tokens.GetRolesAndPermissionsAsync(userId, ct);
        return Results.Ok(user.ToDto(roles, permissions));
    }

    private static async Task<IResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        ChangePasswordRequestValidator validator,
        ClaimsPrincipal principal,
        AppDbContext db,
        IPasswordHasher<User> hasher,
        TokenService tokens,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            return ProblemResults.ValidationProblem(validation);
        }

        var userId = principal.GetUserId();
        var user = await db.Set<User>().SingleAsync(u => u.Id == userId, ct);

        var verifyResult = hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            return ProblemResults.Unauthorized(http, "La contraseña actual no es correcta.");
        }

        user.PasswordHash = hasher.HashPassword(user, request.NewPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        await tokens.RevokeAllForUserAsync(userId, ct);

        return Results.NoContent();
    }

    private static async Task<IResult> ListUsersAsync(
        int? page,
        int? pageSize,
        string? search,
        AppDbContext db,
        TokenService tokens,
        CancellationToken ct)
    {
        var currentPage = page is null or < 1 ? 1 : page.Value;
        var currentPageSize = pageSize is null or < 1 or > 100 ? 20 : pageSize.Value;

        var query = db.Set<User>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(u => u.Email.ToLower().Contains(term) || u.DisplayName.ToLower().Contains(term));
        }

        var total = await query.CountAsync(ct);
        var users = await query
            .OrderBy(u => u.CreatedAt)
            .Skip((currentPage - 1) * currentPageSize)
            .Take(currentPageSize)
            .ToListAsync(ct);

        var items = new List<UserDto>(users.Count);
        foreach (var user in users)
        {
            var (roles, permissions) = await tokens.GetRolesAndPermissionsAsync(user.Id, ct);
            items.Add(user.ToDto(roles, permissions));
        }

        return Results.Ok(new PagedResult<UserDto>(items, currentPage, currentPageSize, total));
    }

    private static async Task<IResult> GetByIdAsync(Guid id, AppDbContext db, TokenService tokens, HttpContext http, CancellationToken ct)
    {
        var user = await db.Set<User>().FindAsync([id], ct);
        if (user is null)
        {
            return ProblemResults.NotFound(http, "Usuario no encontrado.");
        }

        var (roles, permissions) = await tokens.GetRolesAndPermissionsAsync(id, ct);
        return Results.Ok(user.ToDto(roles, permissions));
    }

    private static async Task<IResult> CreateUserAsync(
        CreateUserRequest request,
        CreateUserRequestValidator validator,
        AppDbContext db,
        IPasswordHasher<User> hasher,
        TokenService tokens,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            return ProblemResults.ValidationProblem(validation);
        }

        var normalized = request.Email.Trim().ToLowerInvariant();
        var emailTaken = await db.Set<User>().AnyAsync(u => u.EmailNormalized == normalized, ct);
        if (emailTaken)
        {
            return ProblemResults.Conflict(http, "Ya existe un usuario con ese email.");
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Email = request.Email,
            EmailNormalized = normalized,
            DisplayName = request.DisplayName,
            EmailConfirmed = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password);

        db.Add(user);

        var userRole = await db.Set<Role>().SingleAsync(r => r.Name == SystemRoles.User, ct);
        db.Add(new UserRole { UserId = user.Id, RoleId = userRole.Id, GrantedAt = now });

        await db.SaveChangesAsync(ct);

        var (roles, permissions) = await tokens.GetRolesAndPermissionsAsync(user.Id, ct);
        return Results.Ok(user.ToDto(roles, permissions));
    }

    private static async Task<IResult> UpdateUserAsync(
        Guid id,
        UpdateUserRequest request,
        UpdateUserRequestValidator validator,
        ClaimsPrincipal principal,
        AppDbContext db,
        TokenService tokens,
        HttpContext http,
        CancellationToken ct)
    {
        var validation = validator.Validate(request);
        if (!validation.IsValid)
        {
            return ProblemResults.ValidationProblem(validation);
        }

        if (id == principal.GetUserId())
        {
            return ProblemResults.Forbidden(http, "No puedes editar tu propio usuario desde aquí.");
        }

        var user = await db.Set<User>().FindAsync([id], ct);
        if (user is null)
        {
            return ProblemResults.NotFound(http, "Usuario no encontrado.");
        }

        if (request.DisplayName is not null)
        {
            user.DisplayName = request.DisplayName;
        }

        if (request.IsActive is not null && request.IsActive.Value != user.IsActive)
        {
            if (!request.IsActive.Value && await IsLastAdminAsync(db, id, ct))
            {
                return ProblemResults.Conflict(http, "No se puede desactivar al último usuario con rol Admin.");
            }

            user.IsActive = request.IsActive.Value;
            if (!user.IsActive)
            {
                await tokens.RevokeAllForUserAsync(id, ct);
            }
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var (roles, permissions) = await tokens.GetRolesAndPermissionsAsync(id, ct);
        return Results.Ok(user.ToDto(roles, permissions));
    }

    private static async Task<IResult> DeactivateUserAsync(
        Guid id,
        ClaimsPrincipal principal,
        AppDbContext db,
        TokenService tokens,
        HttpContext http,
        CancellationToken ct)
    {
        if (id == principal.GetUserId())
        {
            return ProblemResults.Forbidden(http, "No puedes desactivar tu propio usuario.");
        }

        var user = await db.Set<User>().FindAsync([id], ct);
        if (user is null)
        {
            return ProblemResults.NotFound(http, "Usuario no encontrado.");
        }

        if (!user.IsActive)
        {
            return Results.NoContent();
        }

        if (await IsLastAdminAsync(db, id, ct))
        {
            return ProblemResults.Conflict(http, "No se puede desactivar al último usuario con rol Admin.");
        }

        user.IsActive = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await tokens.RevokeAllForUserAsync(id, ct);

        return Results.NoContent();
    }

    private static async Task<bool> IsLastAdminAsync(AppDbContext db, Guid userId, CancellationToken ct)
    {
        var adminRole = await db.Set<Role>().SingleOrDefaultAsync(r => r.Name == SystemRoles.Admin, ct);
        if (adminRole is null)
        {
            return false;
        }

        var hasAdminRole = await db.Set<UserRole>().AnyAsync(ur => ur.UserId == userId && ur.RoleId == adminRole.Id, ct);
        if (!hasAdminRole)
        {
            return false;
        }

        var adminCount = await db.Set<UserRole>().CountAsync(ur => ur.RoleId == adminRole.Id, ct);
        return adminCount <= 1;
    }

    private static async Task<IResult> AssignRoleAsync(
        Guid id,
        AssignRoleRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        TokenService tokens,
        IPermissionChecker permissionChecker,
        HttpContext http,
        CancellationToken ct)
    {
        if (!await permissionChecker.HasPermissionAsync(principal.GetUserId(), Permissions.UsersManage, ct))
        {
            return ProblemResults.Forbidden(http, "No tienes permiso para gestionar usuarios.");
        }

        if (id == principal.GetUserId())
        {
            return ProblemResults.Forbidden(http, "No puedes modificar tus propios roles.");
        }

        var user = await db.Set<User>().FindAsync([id], ct);
        if (user is null)
        {
            return ProblemResults.NotFound(http, "Usuario no encontrado.");
        }

        var role = await db.Set<Role>().FindAsync([request.RoleId], ct);
        if (role is null)
        {
            return ProblemResults.NotFound(http, "Rol no encontrado.");
        }

        var alreadyAssigned = await db.Set<UserRole>().AnyAsync(ur => ur.UserId == id && ur.RoleId == role.Id, ct);
        if (alreadyAssigned)
        {
            return ProblemResults.Conflict(http, "El usuario ya tiene este rol.");
        }

        var now = DateTimeOffset.UtcNow;
        db.Add(new UserRole { UserId = id, RoleId = role.Id, GrantedAt = now, GrantedBy = principal.GetUserId() });
        db.Add(new RoleAuditLog
        {
            ActorUserId = principal.GetUserId(),
            TargetUserId = id,
            RoleId = role.Id,
            Action = RoleAuditAction.Granted,
            OccurredAt = now,
        });

        await db.SaveChangesAsync(ct);
        await tokens.RevokeAllForUserAsync(id, ct);

        var (roles, permissions) = await tokens.GetRolesAndPermissionsAsync(id, ct);
        return Results.Ok(user.ToDto(roles, permissions));
    }

    private static async Task<IResult> RemoveRoleAsync(
        Guid id,
        Guid roleId,
        ClaimsPrincipal principal,
        AppDbContext db,
        TokenService tokens,
        IPermissionChecker permissionChecker,
        HttpContext http,
        CancellationToken ct)
    {
        if (!await permissionChecker.HasPermissionAsync(principal.GetUserId(), Permissions.UsersManage, ct))
        {
            return ProblemResults.Forbidden(http, "No tienes permiso para gestionar usuarios.");
        }

        if (id == principal.GetUserId())
        {
            return ProblemResults.Forbidden(http, "No puedes modificar tus propios roles.");
        }

        var userRole = await db.Set<UserRole>().FirstOrDefaultAsync(ur => ur.UserId == id && ur.RoleId == roleId, ct);
        if (userRole is null)
        {
            return ProblemResults.NotFound(http, "El usuario no tiene ese rol.");
        }

        var role = await db.Set<Role>().SingleAsync(r => r.Id == roleId, ct);
        if (role.Name == SystemRoles.Admin)
        {
            var adminCount = await db.Set<UserRole>().CountAsync(ur => ur.RoleId == roleId, ct);
            if (adminCount <= 1)
            {
                return ProblemResults.Conflict(http, "No se puede quitar el rol Admin al último usuario que lo tiene.");
            }
        }

        db.Remove(userRole);
        db.Add(new RoleAuditLog
        {
            ActorUserId = principal.GetUserId(),
            TargetUserId = id,
            RoleId = roleId,
            Action = RoleAuditAction.Revoked,
            OccurredAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(ct);
        await tokens.RevokeAllForUserAsync(id, ct);

        return Results.NoContent();
    }
}
