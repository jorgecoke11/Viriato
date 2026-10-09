namespace Viariato.Modules.Casos.Domain;

public enum CasoEstado
{
    Iniciado,
    EnProgreso,
    Pausado,
    EsperandoRevisionHumana,
    Completado,
    Fallido,
    Cancelado,

    /// <summary>
    /// The Caso's current step is an RPA one that is waiting in the queue for a robot to take it. It becomes
    /// <see cref="EnProgreso"/> the moment a robot claims the step, and goes back to this one each time the Caso moves on to
    /// another step a robot has to take (or one is reprocessed). Appended at the end: states are stored by name.
    /// </summary>
    Pendiente,
}
