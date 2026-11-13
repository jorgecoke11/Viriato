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

    /// <summary>
    /// Sent (with any value) by the launcher — the process that starts robots on demand — when it asks with a robot's key
    /// whether there is work for it. It tells the platform the call is not the robot itself: it does not count as the
    /// robot being connected, or an idle robot that is not running would look like a free copy to the dispatcher.
    /// </summary>
    public const string Lanzador = "X-Viriato-Lanzador";

    /// <summary>
    /// The shared secret of the launcher that runs next to Viriato (a file both containers read; nobody types it). With it the
    /// launcher reads its catalogue and downloads robots. It is never handed to a robot: robots get a pass of their own.
    /// </summary>
    public const string SecretoDelLanzador = "X-Viriato-Lanzador-Secreto";

    /// <summary>Passes the launcher hands its robots start with this, so they are never mistaken for a Despliegue's own key.</summary>
    public const string PrefijoDePase = "rpl_";
}
