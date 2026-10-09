namespace Viariato.Modules.Casos.Orchestration;

/// <summary>
/// Why a Caso is being cancelled. It decides which business estado the Caso ends on: the technical estado is always
/// Cancelado, but what the process shows is different for a person who discarded it and for a platform that cut it off.
/// </summary>
public enum MotivoDeCancelacion
{
    /// <summary>A person (or an action they started) cancelled it. Ends as «Descartado».</summary>
    Manual,

    /// <summary>A step ran past its service's maximum time and the platform cut it. Ends as «Cancelado por exceso de tiempo de ejecución».</summary>
    TiempoMaximo,
}

/// <summary>The business estados the platform provisions itself, per process, the first time a Caso needs one. Admins can
/// rename their display like any other estado, but the codes are fixed: they are what the engine looks for.</summary>
public static class EstadosDeSistema
{
    public const string Descartado = "DESCARTADO";
    public const string CanceladoPorTiempo = "CANCELADO_POR_TIEMPO";

    internal static (string Codigo, string Display) Para(MotivoDeCancelacion motivo) => motivo switch
    {
        MotivoDeCancelacion.TiempoMaximo => (CanceladoPorTiempo, "Cancelado por exceso de tiempo de ejecución"),
        _ => (Descartado, "Descartado"),
    };
}
