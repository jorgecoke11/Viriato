namespace Viariato.Modules.RpaFleet.Domain;

/// <summary>
/// How much the launcher next to Viriato may run at once on its machine. One row (or none: the defaults), edited in
/// RPA → Despliegues → «Lanzador», read by the launcher every round: changing it needs nothing done on the server.
/// </summary>
public sealed class AjustesDelLanzador
{
    /// <summary>The only row's id.</summary>
    public static readonly Guid IdUnico = new("6c0f3a52-5e1b-4f4e-9a0e-1a2b3c4d5e6f");

    public const int MaxRobotsPorDefecto = 2;
    public const int MaxConNavegadorPorDefecto = 1;

    public Guid Id { get; set; } = IdUnico;

    /// <summary>Copies of any robot running at once.</summary>
    public int MaxRobotsALaVez { get; set; } = MaxRobotsPorDefecto;

    /// <summary>Of those, how many with a browser: they are the heavy ones (about 2 GB each).</summary>
    public int MaxConNavegador { get; set; } = MaxConNavegadorPorDefecto;

    public DateTimeOffset UpdatedAt { get; set; }
}
