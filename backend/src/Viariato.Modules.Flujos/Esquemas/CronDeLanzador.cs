using Cronos;

namespace Viariato.Modules.Flujos.Esquemas;

/// <summary>
/// When a launcher is due, from its crontab. Pure functions of the clock, so they are tested without waiting for anything: given "now", the zone,
/// the expressions and the last moment fired, which moment (if any) has to fire now. The calendar arithmetic (months, weekdays, the hour that
/// does not exist or happens twice when the clocks change) is Cronos's.
/// </summary>
public static class CronDeLanzador
{
    public const int MaximoDeToleranciaMinutos = 1440;
    public const int LongitudMaxima = 500;

    /// <summary>Reads one expression per line (a ";" also separates). On failure says which line is wrong and why.</summary>
    public static bool TryLeer(string? texto, out IReadOnlyList<CronExpression> expresiones, out string? error)
    {
        var lineas = (texto ?? string.Empty).Split(['\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var lista = new List<CronExpression>();
        foreach (var linea in lineas)
        {
            try
            {
                lista.Add(CronExpression.Parse(linea, CronFormat.Standard));
            }
            catch (CronFormatException ex)
            {
                expresiones = [];
                error = $"«{linea}» no es una expresión cron válida de cinco campos (minuto hora día-del-mes mes día-de-la-semana): {ex.Message}";
                return false;
            }
        }

        expresiones = lista;
        error = lista.Count == 0 ? "Escribe al menos una expresión cron, p. ej. 0 10 * * 1-5 (los días laborables a las 10:00)." : null;
        return lista.Count > 0;
    }

    /// <summary>The zone with that IANA (or Windows) name, or null if this machine does not know it.</summary>
    public static TimeZoneInfo? BuscarZona(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }

    /// <summary>
    /// The moment to fire right now, or null. Of the moments that have arrived, are not older than <paramref name="tolerancia"/> and come after the
    /// last one fired, only the <b>latest</b> counts: if two went by while nothing was looking, one case is opened, not two.
    /// </summary>
    public static DateTimeOffset? Pendiente(
        DateTimeOffset ahora, TimeZoneInfo zona, IReadOnlyList<CronExpression> expresiones, DateTimeOffset? ultimo, TimeSpan tolerancia)
    {
        var desde = ahora - tolerancia;
        if (ultimo is { } u && u > desde) desde = u;

        DateTimeOffset? elegido = null;
        foreach (var expresion in expresiones)
        {
            // fromInclusive false when resuming after the last one fired, so that moment is not taken again.
            var inclusivo = ultimo is not { } l || l < ahora - tolerancia;
            foreach (var momento in expresion.GetOccurrences(desde, ahora, zona, fromInclusive: inclusivo, toInclusive: true))
            {
                var utc = momento.ToUniversalTime();
                if (elegido is null || utc > elegido) elegido = utc;
            }
        }

        return elegido;
    }

    /// <summary>The next moment after <paramref name="ahora"/> it will fire, for showing next to the launcher; null if none ever matches.</summary>
    public static DateTimeOffset? Proximo(DateTimeOffset ahora, TimeZoneInfo zona, IReadOnlyList<CronExpression> expresiones)
    {
        DateTimeOffset? siguiente = null;
        foreach (var expresion in expresiones)
        {
            var candidato = expresion.GetNextOccurrence(ahora, zona);
            if (candidato is { } c && (siguiente is null || c < siguiente)) siguiente = c.ToUniversalTime();
        }

        return siguiente;
    }
}
