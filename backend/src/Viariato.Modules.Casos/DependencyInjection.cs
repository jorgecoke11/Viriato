using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Contracts;
using Viariato.Modules.Casos.Orchestration;
using Viariato.Modules.Casos.Orchestration.Ejecutores;
using Viariato.Modules.Casos.Persistence;
using Viariato.Modules.Casos.Storage;
using Viariato.Modules.Casos.Validation;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Casos;

public static class DependencyInjection
{
    public static IServiceCollection AddCasosModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModuleModelConfiguration, CasosModuleModelConfiguration>();

        services.AddScoped<IDocumentStorageResolver, DocumentStorageResolver>();

        services.AddScoped<IEjecucionOrchestrator, EjecucionOrchestrator>();
        services.AddKeyedScoped<IPasoEjecutor, RpaPasoEjecutor>(TipoPaso.Rpa);
        services.AddKeyedScoped<IPasoEjecutor, AgentePasoEjecutor>(TipoPaso.Agente);
        services.AddKeyedScoped<IPasoEjecutor, ApiPasoEjecutor>(TipoPaso.Api);
        services.AddKeyedScoped<IPasoEjecutor, InternoPasoEjecutor>(TipoPaso.Interno);
        services.AddKeyedScoped<IPasoEjecutor, DecisionPasoEjecutor>(TipoPaso.Decision);
        services.AddKeyedScoped<IPasoEjecutor, EsperaPasoEjecutor>(TipoPaso.Espera);
        services.AddKeyedScoped<IPasoEjecutor, RevisionHumanaPasoEjecutor>(TipoPaso.RevisionHumana);

        services.AddSingleton<StartCasoRequestValidator>();
        services.AddSingleton<UpdateCasoDatosRequestValidator>();
        services.AddSingleton<ResolverRevisionRequestValidator>();
        services.AddSingleton<IValidator<CreateTipoDocumentoRequest>, CreateTipoDocumentoRequestValidator>();
        services.AddSingleton<IValidator<UpdateTipoDocumentoRequest>, UpdateTipoDocumentoRequestValidator>();
        services.AddSingleton<CreateDocumentoClasificacionRequestValidator>();
        services.AddSingleton<UpdateDocumentoClasificacionRequestValidator>();

        return services;
    }
}
