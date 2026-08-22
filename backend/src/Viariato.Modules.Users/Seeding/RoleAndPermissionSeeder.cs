using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Viariato.Infrastructure;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Authorization;

namespace Viariato.Modules.Users.Seeding;

public sealed class RoleAndPermissionSeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<RoleAndPermissionSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await SeedPermissionsAsync(db, cancellationToken);
        await SeedRolesAsync(db, cancellationToken);
        await BootstrapAdminAsync(scope.ServiceProvider, db, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedPermissionsAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.Set<Permission>().ToListAsync(ct);
        var existingNames = existing.Select(p => p.Name).ToHashSet();

        foreach (var name in Permissions.All)
        {
            if (existingNames.Contains(name))
            {
                continue;
            }

            var parts = name.Split('.', 2);
            db.Add(new Permission
            {
                Name = name,
                Module = parts[0],
                Action = parts.Length > 1 ? parts[1] : name,
            });
        }

        var unknown = existingNames.Except(Permissions.All).ToList();
        if (unknown.Count > 0)
        {
            logger.LogWarning("Found permissions in the database that are no longer declared in code: {Permissions}", string.Join(", ", unknown));
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task SeedRolesAsync(AppDbContext db, CancellationToken ct)
    {
        var adminRole = await db.Set<Role>().SingleOrDefaultAsync(r => r.Name == SystemRoles.Admin, ct);
        if (adminRole is null)
        {
            adminRole = new Role
            {
                Name = SystemRoles.Admin,
                IsSystem = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Add(adminRole);
            await db.SaveChangesAsync(ct);
        }

        var userRole = await db.Set<Role>().SingleOrDefaultAsync(r => r.Name == SystemRoles.User, ct);
        if (userRole is null)
        {
            db.Add(new Role
            {
                Name = SystemRoles.User,
                IsSystem = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        var allPermissionIds = await db.Set<Permission>().Select(p => p.Id).ToListAsync(ct);
        var adminPermissionIds = await db.Set<RolePermission>()
            .Where(rp => rp.RoleId == adminRole.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync(ct);

        foreach (var permissionId in allPermissionIds.Except(adminPermissionIds))
        {
            db.Add(new RolePermission { RoleId = adminRole.Id, PermissionId = permissionId });
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task BootstrapAdminAsync(IServiceProvider services, AppDbContext db, CancellationToken ct)
    {
        var hasAdmin = await db.Set<UserRole>()
            .Include(ur => ur.Role)
            .AnyAsync(ur => ur.Role.Name == SystemRoles.Admin, ct);

        if (hasAdmin)
        {
            return;
        }

        var email = configuration["Bootstrap:AdminEmail"];
        var password = configuration["Bootstrap:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "No admin user exists and Bootstrap__AdminEmail / Bootstrap__AdminPassword are not set. Skipping admin bootstrap.");
            return;
        }

        var adminRole = await db.Set<Role>().SingleAsync(r => r.Name == SystemRoles.Admin, ct);
        var hasher = services.GetRequiredService<IPasswordHasher<User>>();

        var user = new User
        {
            Email = email,
            EmailNormalized = email.Trim().ToLowerInvariant(),
            DisplayName = "Admin",
            EmailConfirmed = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        user.PasswordHash = hasher.HashPassword(user, password);

        db.Add(user);
        db.Add(new UserRole
        {
            UserId = user.Id,
            RoleId = adminRole.Id,
            GrantedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(ct);

        logger.LogWarning("Bootstrapped initial admin user {Email}", email);
    }
}
