using Viariato.Modules.RpaFleet.Domain;

namespace Viariato.Modules.Casos.Despacho;

/// <summary>The oldest waiting step one robot of a machine could take next.</summary>
/// <param name="DespliegueId">The robot (Despliegue) that would take it.</param>
/// <param name="ServicioId">Its service: what the machine's order ranks.</param>
/// <param name="DetalleId">The queue entry itself.</param>
/// <param name="EnEsperaDesde">When the step started waiting; the older, the sooner, among equals.</param>
internal sealed record Candidato(Guid DespliegueId, Guid ServicioId, Guid DetalleId, DateTimeOffset EnEsperaDesde);

/// <summary>
/// Which of the things waiting on a machine goes next. Pure on purpose: it is given what is waiting and the machine's
/// configuration and answers, so every rule can be checked without a database.
///
/// The rules, in order:
/// <list type="number">
/// <item>A service is ranked by its place in the machine's order; one the machine runs but did not list is last.
/// With <see cref="PoliticaDespacho.Turnos"/> the ranking is a rotation that starts after the service served last.</item>
/// <item>The better rank goes first. Among equals — two robots of the same service, or services nobody ranked —
/// whatever has waited longest goes first, so the default (no order configured) is simply first come, first served.</item>
/// </list>
/// </summary>
internal static class Despachador
{
    /// <summary>The place of <paramref name="servicioId"/> in the dispatch order: 0 goes first, larger values later.</summary>
    public static int Rango(Guid servicioId, IReadOnlyList<Guid> orden, PoliticaDespacho politica, Guid? ultimoServicioId)
    {
        var posicion = IndexOf(orden, servicioId);
        if (posicion < 0) return int.MaxValue;
        if (politica == PoliticaDespacho.Prioridad) return posicion;

        // Rotation: the service right after the one served last is first, the one served last is the last in line.
        // If the last one is not in the list (or nothing was served yet) it simply starts from the top.
        var posicionUltimo = ultimoServicioId is { } ultimo ? IndexOf(orden, ultimo) : -1;
        var n = orden.Count;
        return (((posicion - posicionUltimo - 1) % n) + n) % n;
    }

    /// <summary>Everything waiting, in the order the machine would serve it.</summary>
    public static IReadOnlyList<Candidato> Ordenar(
        IEnumerable<Candidato> candidatos, IReadOnlyList<Guid> orden, PoliticaDespacho politica, Guid? ultimoServicioId) =>
        candidatos
            .OrderBy(c => Rango(c.ServicioId, orden, politica, ultimoServicioId))
            .ThenBy(c => c.EnEsperaDesde)
            // Version-7 ids are time-ordered, and their text form sorts like their bytes; the Guid comparison does not.
            .ThenBy(c => c.DetalleId.ToString(), StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Everything waiting, in the order the machine would hand it out if its robots kept asking. Each queue (one per
    /// robot) arrives already ordered inside — by the priority of its executions, then by age — and the machine's order of
    /// services only decides which queue's front goes next, so a high-priority execution never jumps ahead of a service the
    /// machine ranks higher.
    /// </summary>
    public static IReadOnlyList<T> Servir<T>(
        IEnumerable<IReadOnlyList<T>> colas,
        Func<T, Candidato> comoCandidato,
        IReadOnlyList<Guid> orden,
        PoliticaDespacho politica,
        Guid? ultimoServicioId)
    {
        var restantes = colas.Select(c => new Queue<T>(c)).Where(q => q.Count > 0).ToList();
        var servidos = new List<T>();
        var ultimo = ultimoServicioId;

        while (restantes.Count > 0)
        {
            var frentes = restantes.Select(q => (Cola: q, Candidato: comoCandidato(q.Peek()))).ToList();
            var elegido = Elegir(frentes.Select(f => f.Candidato), orden, politica, ultimo)!;
            var cola = frentes.First(f => f.Candidato.DetalleId == elegido.DetalleId).Cola;

            servidos.Add(cola.Dequeue());
            if (cola.Count == 0) restantes.Remove(cola);
            ultimo = elegido.ServicioId;
        }

        return servidos;
    }

    /// <summary>What goes next, or null if nothing is waiting.</summary>
    public static Candidato? Elegir(
        IEnumerable<Candidato> candidatos, IReadOnlyList<Guid> orden, PoliticaDespacho politica, Guid? ultimoServicioId) =>
        Ordenar(candidatos, orden, politica, ultimoServicioId).FirstOrDefault();

    private static int IndexOf(IReadOnlyList<Guid> orden, Guid id)
    {
        for (var i = 0; i < orden.Count; i++)
        {
            if (orden[i] == id) return i;
        }

        return -1;
    }
}
