using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Viariato.Infrastructure;
using Viariato.Modules.Users.Domain;

namespace Viariato.Modules.Users.Authorization;

/// <summary>
/// Revalidates permissions directly against the database, bypassing the JWT "perm" claims.
/// Access tokens live for 15 minutes, so a revoked role would otherwise stay effective until
/// the token expires; this cache is short (30s) purely to absorb repeated calls within one request burst.
/// </summary>
public sealed class PermissionChecker(AppDbContext db, IMemoryCache cache) : IPermissionChecker
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public async Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken ct)
    {
        var cacheKey = $"perm:{userId}:{permission}";
        if (cache.TryGetValue(cacheKey, out bool cached))
        {
            return cached;
        }

        var roleIds = await db.Set<UserRole>()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(ct);

        var hasPermission = await db.Set<RolePermission>()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .AnyAsync(rp => rp.Permission.Name == permission, ct);

        cache.Set(cacheKey, hasPermission, CacheDuration);
        return hasPermission;
    }
}
