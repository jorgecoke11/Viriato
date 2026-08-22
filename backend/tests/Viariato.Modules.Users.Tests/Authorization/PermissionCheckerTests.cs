using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Viariato.Modules.Users.Authorization;
using Viariato.Modules.Users.Domain;
using Xunit;

namespace Viariato.Modules.Users.Tests.Authorization;

public sealed class PermissionCheckerTests
{
    private static User NewUser() => new()
    {
        Email = "user@example.com",
        EmailNormalized = "user@example.com",
        DisplayName = "Test User",
        PasswordHash = "irrelevant-for-these-tests",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task HasPermissionAsync_ReturnsTrue_WhenAUserRoleGrantsThePermission()
    {
        using var db = TestDbContextFactory.Create();
        var checker = new PermissionChecker(db, new MemoryCache(new MemoryCacheOptions()));

        var permission = new Permission { Name = "users.read", Module = "users", Action = "read" };
        var role = new Role { Name = "Reader", IsSystem = false, CreatedAt = DateTimeOffset.UtcNow };
        var user = NewUser();
        db.AddRange(permission, role, user);
        db.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        db.Add(new UserRole { UserId = user.Id, RoleId = role.Id, GrantedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var result = await checker.HasPermissionAsync(user.Id, "users.read", default);

        Assert.True(result);
    }

    [Fact]
    public async Task HasPermissionAsync_ReturnsFalse_WhenNoRoleGrantsThePermission()
    {
        using var db = TestDbContextFactory.Create();
        var checker = new PermissionChecker(db, new MemoryCache(new MemoryCacheOptions()));
        var user = NewUser();
        db.Add(user);
        await db.SaveChangesAsync();

        var result = await checker.HasPermissionAsync(user.Id, "users.read", default);

        Assert.False(result);
    }

    [Fact]
    public async Task HasPermissionAsync_ReturnsFalse_ForAnUnknownUser()
    {
        using var db = TestDbContextFactory.Create();
        var checker = new PermissionChecker(db, new MemoryCache(new MemoryCacheOptions()));

        var result = await checker.HasPermissionAsync(Guid.CreateVersion7(), "users.read", default);

        Assert.False(result);
    }

    [Fact]
    public async Task HasPermissionAsync_CachesTheResultFor30Seconds_SoARevocationIsNotSeenImmediately()
    {
        using var db = TestDbContextFactory.Create();
        var checker = new PermissionChecker(db, new MemoryCache(new MemoryCacheOptions()));

        var permission = new Permission { Name = "users.read", Module = "users", Action = "read" };
        var role = new Role { Name = "Reader", IsSystem = false, CreatedAt = DateTimeOffset.UtcNow };
        var user = NewUser();
        var rolePermission = new RolePermission { RoleId = role.Id, PermissionId = permission.Id };
        db.AddRange(permission, role, user, rolePermission);
        db.Add(new UserRole { UserId = user.Id, RoleId = role.Id, GrantedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var beforeRevocation = await checker.HasPermissionAsync(user.Id, "users.read", default);
        Assert.True(beforeRevocation);

        // Revoke the permission directly, bypassing the checker's cache.
        db.Remove(rolePermission);
        await db.SaveChangesAsync();

        var immediatelyAfterRevocation = await checker.HasPermissionAsync(user.Id, "users.read", default);
        Assert.True(immediatelyAfterRevocation);
    }

    [Fact]
    public async Task HasPermissionAsync_ReflectsARevocation_OnceTheCacheEntryIsGone()
    {
        using var db = TestDbContextFactory.Create();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var checker = new PermissionChecker(db, cache);

        var permission = new Permission { Name = "users.read", Module = "users", Action = "read" };
        var role = new Role { Name = "Reader", IsSystem = false, CreatedAt = DateTimeOffset.UtcNow };
        var user = NewUser();
        var rolePermission = new RolePermission { RoleId = role.Id, PermissionId = permission.Id };
        db.AddRange(permission, role, user, rolePermission);
        db.Add(new UserRole { UserId = user.Id, RoleId = role.Id, GrantedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        await checker.HasPermissionAsync(user.Id, "users.read", default);

        db.Remove(rolePermission);
        await db.SaveChangesAsync();
        cache.Remove($"perm:{user.Id}:users.read");

        var afterCacheExpiry = await checker.HasPermissionAsync(user.Id, "users.read", default);

        Assert.False(afterCacheExpiry);
    }
}
