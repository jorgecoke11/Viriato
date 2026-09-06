using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Viariato.Modules.Ops;

public static class DependencyInjection
{
    // No module-specific services: Trabajo/TrabajoLog and ITrabajoTracker are registered by
    // AddInfrastructure, since this module only exposes read endpoints over them. Kept as an
    // extension method for the same AddXModule/MapXEndpoints wiring shape as every other module.
    public static IServiceCollection AddOpsModule(this IServiceCollection services, IConfiguration configuration) =>
        services;
}
