namespace Viariato.Modules.Casos.Domain;

public enum EjecucionEstado
{
    Pendiente,
    EnProgreso,
    Pausada,
    EsperandoRevisionHumana,
    Completada,
    Fallida,
    Cancelada,
}
