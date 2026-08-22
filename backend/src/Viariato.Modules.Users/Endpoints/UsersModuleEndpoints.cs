using Microsoft.AspNetCore.Routing;

namespace Viariato.Modules.Users.Endpoints;

public static class UsersModuleEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAuthEndpoints();
        endpoints.MapUsersManagementEndpoints();
        endpoints.MapRolesEndpoints();

        return endpoints;
    }
}
