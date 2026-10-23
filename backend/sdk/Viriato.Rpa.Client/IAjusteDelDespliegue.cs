using Viariato.ApiContracts;

namespace Viriato.Rpa.Client;

/// <summary>
/// Something on the robot's machine that follows what Viriato decided for this Despliegue — the live view of its screen is the first.
/// <see cref="RpaWorker"/> hands every implementation registered in the container the Despliegue's state each time it reads it (every
/// poll while idle, and between steps), so a change made in Viriato reaches the robot within seconds without restarting it.
/// Implementations compare with what they already applied and do nothing when it is the same. A failure here is logged and ignored:
/// following a setting must never stop the robot from working.
/// </summary>
public interface IAjusteDelDespliegue
{
    Task AplicarAsync(DespliegueEstadoDto estado, CancellationToken ct);
}
