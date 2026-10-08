namespace Viariato.Modules.RpaFleet.Domain;

/// <summary>How a machine chooses between services that all have work waiting.</summary>
public enum PoliticaDespacho
{
    /// <summary>The order is a priority: the first service in the list with work waiting goes first, every time.
    /// Predictable, but a service low in the list waits for as long as the ones above it keep having work.</summary>
    Prioridad = 0,

    /// <summary>The order is a rotation: after a service has had its turn, the next one in the list that has work
    /// waiting goes, and after the last it starts over. Nobody starves.</summary>
    Turnos = 1,
}
