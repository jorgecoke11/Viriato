namespace Viariato.ApiContracts;

/// <summary>Headers the robot client and the API agree on, besides the API key.</summary>
public static class RpaHeaders
{
    /// <summary>
    /// Identifies one running copy of a robot (a container replica, or one more process on Windows started with the same
    /// API key). The client generates it when it starts and sends it on every request; the platform never configures it.
    /// Without it, every copy of a Despliegue is one and the same robot to the dispatcher, which is how robots behaved before
    /// instances existed — so a stuck copy would hold up the others.
    /// </summary>
    public const string Instancia = "X-Viriato-Instancia";
}
