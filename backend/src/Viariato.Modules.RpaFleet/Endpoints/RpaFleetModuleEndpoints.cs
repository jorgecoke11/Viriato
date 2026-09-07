using Microsoft.AspNetCore.Routing;

namespace Viariato.Modules.RpaFleet.Endpoints;

public static class RpaFleetModuleEndpoints
{
    public static IEndpointRouteBuilder MapRpaFleetEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapEquiposEndpoints();
        endpoints.MapServiciosEndpoints();
        endpoints.MapDespliguesEndpoints();

        return endpoints;
    }
}
