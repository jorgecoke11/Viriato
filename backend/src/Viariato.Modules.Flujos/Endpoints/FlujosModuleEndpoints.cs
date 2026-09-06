using Microsoft.AspNetCore.Routing;

namespace Viariato.Modules.Flujos.Endpoints;

public static class FlujosModuleEndpoints
{
    public static IEndpointRouteBuilder MapFlujosEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapFlujosCrudEndpoints();
        endpoints.MapFlujoVersionesEndpoints();
        endpoints.MapFlujoEstadosEndpoints();
        endpoints.MapFlujoTiposCasoEndpoints();
        endpoints.MapAgentesEndpoints();
        endpoints.MapAsignacionesEndpoints();

        return endpoints;
    }
}
