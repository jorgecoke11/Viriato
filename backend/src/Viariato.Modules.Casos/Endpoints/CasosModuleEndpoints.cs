using Microsoft.AspNetCore.Routing;

namespace Viariato.Modules.Casos.Endpoints;

public static class CasosModuleEndpoints
{
    public static IEndpointRouteBuilder MapCasosEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCasosCoreEndpoints();
        endpoints.MapDocumentosEndpoints();
        endpoints.MapEvidenciasEndpoints();
        endpoints.MapRevisionesEndpoints();

        return endpoints;
    }
}
