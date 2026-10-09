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

        var permisosNuevos = await SeedPermissionsAsync(db, cancellationToken);
        await SeedRolesAsync(db, permisosNuevos, cancellationToken);
        await BootstrapAdminAsync(scope.ServiceProvider, db, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Returns the permissions that did not exist until now, which is when the roles may be given a default for them.</summary>
    private async Task<HashSet<string>> SeedPermissionsAsync(AppDbContext db, CancellationToken ct)
    {
        var creados = new HashSet<string>();
        var existing = await db.Set<Permission>().ToListAsync(ct);
        var existingNames = existing.Select(p => p.Name).ToHashSet();

        foreach (var name in Permissions.All)
        {
            if (existingNames.Contains(name))
            {
                continue;
            }

            creados.Add(name);
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
        return creados;
    }

    private async Task SeedRolesAsync(AppDbContext db, IReadOnlySet<string> permisosNuevos, CancellationToken ct)
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
            userRole = new Role
            {
                Name = SystemRoles.User,
                IsSystem = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Add(userRole);
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

        await GrantUserDefaultsAsync(db, userRole, permisosNuevos, ct);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>The rule on its own, so it can be tested: a role with no permission gets the whole default list; one that already has
    /// some only gets the defaults that were created just now.</summary>
    public static IReadOnlyList<string> PermisosPorDefectoDelRolUser(bool elRolYaTienePermisos, IReadOnlySet<string> permisosNuevos) =>
        elRolYaTienePermisos
            ? Permissions.ParaElRolUser.Where(permisosNuevos.Contains).ToList()
            : Permissions.ParaElRolUser.ToList();

    /// <summary>
    /// The <c>User</c> role is the one people get on registering, so it has to be able to use the platform out of the box: it is
    /// given <see cref="Permissions.ParaElRolUser"/> when it has no permission at all (a role nobody has configured yet), and later,
    /// each permission of that list that is *new* — so a release that adds one reaches it too. Whatever an administrator has
    /// taken away or added in between is left alone: a permission that already existed is never given back.
    /// </summary>
    private static async Task GrantUserDefaultsAsync(AppDbContext db, Role userRole, IReadOnlySet<string> permisosNuevos, CancellationToken ct)
    {
        var tienePermisos = await db.Set<RolePermission>().AnyAsync(rp => rp.RoleId == userRole.Id, ct);
        var quePorDefecto = PermisosPorDefectoDelRolUser(tienePermisos, permisosNuevos);
        if (quePorDefecto.Count == 0) return;

        var ids = await db.Set<Permission>().Where(p => quePorDefecto.Contains(p.Name)).Select(p => p.Id).ToListAsync(ct);
        var yaTiene = await db.Set<RolePermission>().Where(rp => rp.RoleId == userRole.Id).Select(rp => rp.PermissionId).ToListAsync(ct);
        foreach (var permissionId in ids.Except(yaTiene))
        {
            db.Add(new RolePermission { RoleId = userRole.Id, PermissionId = permissionId });
        }
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
