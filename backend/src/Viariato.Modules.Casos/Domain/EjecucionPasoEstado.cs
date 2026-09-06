namespace Viariato.Modules.Casos.Domain;

public enum EjecucionPasoEstado
{
    Pendiente,
    EnProgreso,
    EsperandoRevisionHumana,
    Completado,
    Fallido,
    /// <summary>A Decision branch that was not taken.</summary>
    Omitido,
    Cancelado,
}
