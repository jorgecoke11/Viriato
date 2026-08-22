using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Viariato.Infrastructure;
using Viariato.Modules.Users.Auth;
using Viariato.Modules.Users.Authorization;
using Viariato.Modules.Users.Contracts;
using Viariato.Modules.Users.Domain;
using Viariato.Modules.Users.Persistence;
using Viariato.Modules.Users.Seeding;
using Viariato.Modules.Users.Validation;

namespace Viariato.Modules.Users;

public static class DependencyInjection
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModuleModelConfiguration, UsersModuleModelConfiguration>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddHostedService<RoleAndPermissionSeeder>();

        services.AddScoped<TokenService>();
        services.AddMemoryCache();
        services.AddScoped<IPermissionChecker, PermissionChecker>();

        services.AddSingleton<RegisterRequestValidator>();
        services.AddSingleton<LoginRequestValidator>();
        services.AddSingleton<UpdateProfileRequestValidator>();
        services.AddSingleton<ChangePasswordRequestValidator>();
        services.AddSingleton<IValidator<CreateRoleRequest>, CreateRoleRequestValidator>();

        return services;
    }
}
