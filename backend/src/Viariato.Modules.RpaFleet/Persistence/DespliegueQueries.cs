using Microsoft.EntityFrameworkCore;
using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.RpaFleet.Persistence;

public static class DespliegueQueries
{
    /// <summary>Its machine, its service and that service's versions: what its DTO and the launcher's catalogue read
    /// (<see cref="Despliegue.VersionQueCorre"/>).</summary>
    public static IQueryable<Despliegue> ConSuRobot(this IQueryable<Despliegue> query) =>
        query.Include(d => d.Equipo).Include(d => d.Servicio).ThenInclude(s => s!.Versiones);
}
