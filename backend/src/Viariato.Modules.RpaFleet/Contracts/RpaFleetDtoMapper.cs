using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.RpaFleet.Contracts;

public static class RpaFleetDtoMapper
{
    public static EquipoDto ToDto(this Equipo equipo) => new(
        equipo.Id, equipo.Nombre, equipo.Descripcion, equipo.Activo, equipo.CreatedAt, equipo.UpdatedAt);

    public static ServicioDto ToDto(this Servicio servicio) => new(
        servicio.Id, servicio.Nombre, servicio.Descripcion, servicio.Activo, servicio.MaxEjecucionesGlobales, servicio.TiempoMaximoMinutos, servicio.CreatedAt, servicio.UpdatedAt,
        servicio.UltimaVersion?.Numero);

    public static DespliegueDto ToDto(this Despliegue despliegue) => new(
        despliegue.Id,
        despliegue.EquipoId,
        despliegue.Equipo?.Nombre ?? string.Empty,
        despliegue.ServicioId,
        despliegue.Servicio?.Nombre ?? string.Empty,
        despliegue.FlujoId,
        despliegue.FlujoDestinoId,
        despliegue.Encendido,
        despliegue.ApiKeyPrefix,
        despliegue.CreatedAt,
        despliegue.UpdatedAt,
        despliegue.LastUsedAt,
        despliegue.VistaEnDirecto.ToString(),
        despliegue.GrabacionDeVideo.ToString(),
        despliegue.VersionQueCorre?.Numero,
        despliegue.Replicas,
        despliegue.MemoriaMaximaMb,
        despliegue.EnElLanzador,
        despliegue.VersionDeRobotId);

    public static VersionDeRobotDto ToDto(this VersionDeRobot version, bool ultima, IReadOnlyList<string> usadaPor) => new(
        version.Id,
        version.Numero,
        version.NombreArchivo,
        version.TamanoBytes,
        string.Join(' ', new[] { version.Comando }.Concat(version.Argumentos)),
        version.Pantalla,
        version.MemoriaMaximaMb,
        version.SubidaPor,
        version.CreatedAt,
        ultima,
        usadaPor);

    /// <summary>What the robot of this Despliegue is told to do with its screen (GET /rpa/despliegue).</summary>
    public static Viariato.ApiContracts.PantallaDelRobotDto ToPantallaDto(this Despliegue despliegue) => new(
        VistaEnDirectoPresets.ParaElRobot(despliegue.VistaEnDirecto),
        despliegue.GrabacionDeVideo switch
        {
            GrabacionDeVideo.Activa => true,
            GrabacionDeVideo.Apagada => false,
            _ => null,
        });
}
