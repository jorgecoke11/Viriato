using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.RpaFleet.Contracts;

public static class RpaFleetDtoMapper
{
    public static EquipoDto ToDto(this Equipo equipo) => new(
        equipo.Id, equipo.Nombre, equipo.Descripcion, equipo.Activo, equipo.CreatedAt, equipo.UpdatedAt);

    public static ServicioDto ToDto(this Servicio servicio) => new(
        servicio.Id, servicio.Nombre, servicio.Descripcion, servicio.Activo, servicio.CreatedAt, servicio.UpdatedAt);

    public static DespliegueDto ToDto(this Despliegue despliegue) => new(
        despliegue.Id,
        despliegue.EquipoId,
        despliegue.Equipo?.Nombre ?? string.Empty,
        despliegue.ServicioId,
        despliegue.Servicio?.Nombre ?? string.Empty,
        despliegue.FlujoId,
        despliegue.Encendido,
        despliegue.ApiKeyPrefix,
        despliegue.CreatedAt,
        despliegue.UpdatedAt,
        despliegue.LastUsedAt);
}
