namespace Viariato.ApiContracts;

/// <summary>Mirrors Viariato.Modules.Casos.Domain.EvidenciaTipo's string values exactly — the worker
/// endpoint parses this the same way the existing user-facing evidencias endpoint does.</summary>
public enum EvidenciaTipo
{
    Screenshot,
    Video,
    ArchivoGenerado,
    DatosExtraidos,
    Otro,
}
