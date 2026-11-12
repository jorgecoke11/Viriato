namespace Viariato.Modules.RpaFleet.Auth;

public sealed record DespliegueIdentity(Guid DespliegueId, Guid EquipoId, Guid ServicioId, Guid FlujoId, bool Encendido);

public interface IDespliegueApiKeyValidator
{
    /// <summary>Null means the key doesn't match any Despliegue. A disabled Despliegue still
    /// authenticates (Encendido = false) — callers that need it switched on check that themselves,
    /// e.g. so GET /rpa/despliegue can report "you're off" instead of just failing outright.</summary>
    /// <param name="registrarUso">False for a call that is not the robot itself (the launcher asking on its behalf): the key is
    /// checked but the Despliegue is not marked as used, which is what makes a robot count as connected.</param>
    Task<DespliegueIdentity?> ValidateAsync(string rawKey, CancellationToken ct, bool registrarUso = true);
}
