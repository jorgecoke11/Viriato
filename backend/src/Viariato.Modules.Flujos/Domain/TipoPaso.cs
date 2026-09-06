namespace Viariato.Modules.Flujos.Domain;

/// <summary>What kind of work a <see cref="FlujoPasoDef"/> represents — lets one Flujo mix RPA,
/// agentic and plain-API steps without the orchestrator needing to know their internals.</summary>
public enum TipoPaso
{
    Rpa,
    Agente,
    Api,
    Interno,
    Decision,
    Espera,
    RevisionHumana,
}
