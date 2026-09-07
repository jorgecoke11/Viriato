using Microsoft.AspNetCore.Routing;

namespace Viariato.Modules.Casos.Endpoints;

public static class CasosModuleEndpoints
{
    public static IEndpointRouteBuilder MapCasosEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapCasosCoreEndpoints();
        endpoints.MapDocumentosEndpoints();
        endpoints.MapTiposDocumentoEndpoints();
        endpoints.MapDocumentoClasificacionesEndpoints();
        endpoints.MapEvidenciasEndpoints();
        endpoints.MapRevisionesEndpoints();
        endpoints.MapRpaWorkerEndpoints();

        return endpoints;
    }
}
