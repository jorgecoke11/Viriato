namespace Viariato.Modules.RpaFleet.Auth;

public sealed record DespliegueIdentity(Guid DespliegueId, Guid EquipoId, Guid ServicioId, Guid FlujoId, bool Encendido);

public interface IDespliegueApiKeyValidator
{
    /// <summary>Null means the key doesn't match any Despliegue. A disabled Despliegue still
    /// authenticates (Encendido = false) — callers that need it switched on check that themselves,
    /// e.g. so GET /rpa/despliegue can report "you're off" instead of just failing outright.</summary>
    Task<DespliegueIdentity?> ValidateAsync(string rawKey, CancellationToken ct);
}
