namespace Viariato.Modules.Casos.Domain;

/// <summary>
/// Something a process's robots remember from one case to the next (the last price of a product, the last page read…): a JSON
/// value under a key, per process. A robot only ever sees the ones of the process its key belongs to. Generic on purpose, so a
/// robot that needs memory does not need a module of its own.
/// </summary>
public sealed class DatoDeProceso
{
    public const int LongitudMaximaClave = 200;

    /// <summary>64 KB of JSON: enough for a history of hundreds of readings, small enough that it never weighs on the database.</summary>
    public const int TamanoMaximoValor = 64 * 1024;

    public Guid FlujoId { get; set; }

    public required string Clave { get; set; }

    /// <summary>jsonb.</summary>
    public required string ValorJson { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
