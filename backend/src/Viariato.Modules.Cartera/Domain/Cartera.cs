namespace Viariato.Modules.Cartera.Domain;

/// <summary>
/// The one simulated portfolio ("de papel") of the trading robots: how much it started with. There is a single row. The cash and the positions are
/// not stored: they are what the movements add up to, so there is nothing to drift out of step with them.
/// </summary>
public sealed class CarteraEstado
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public decimal CapitalInicialEur { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>One simulated order that was executed. <c>CasoId</c> is the case that asked for it and is unique: it is what makes executing the same case twice harmless.</summary>
public sealed class CarteraMovimiento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CasoId { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
    /// <summary>"Compra" or "Venta".</summary>
    public string Lado { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal PrecioUsd { get; set; }
    public decimal EurUsd { get; set; }
    public decimal ImporteEur { get; set; }
    public decimal ComisionEur { get; set; }
    public decimal EfectivoTrasEur { get; set; }
    /// <summary>The stop that came with a purchase (the price at which the position should be closed); null for a sale or a purchase without one.</summary>
    public decimal? StopUsd { get; set; }
    public DateTimeOffset Fecha { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>The last price the robots saw for a value (and the euro-dollar rate then): what the positions are valued at on screen.</summary>
public sealed class CarteraCotizacion
{
    public string Ticker { get; set; } = string.Empty;
    public decimal PrecioUsd { get; set; }
    public decimal EurUsd { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// One analysis of a value, kept so that it can be judged later: what the rules said, how each rule voted, and — once the days have passed — what the price did.
/// This is what the trading robots learn from. <c>CasoId</c> is the analysis case and is unique, so registering the same analysis twice is harmless.
/// </summary>
public sealed class CarteraSenal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CasoId { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
    /// <summary>The day of the last price the analysis used.</summary>
    public DateOnly Fecha { get; set; }
    public decimal PrecioUsd { get; set; }
    /// <summary>"Comprar", "Vender" or "Mantener".</summary>
    public string Senal { get; set; } = string.Empty;
    public decimal Puntos { get; set; }
    /// <summary>What each rule voted, as JSON ({"tendencia":2,"rsi":0,"macd":1,"bollinger":0}).</summary>
    public string VotosJson { get; set; } = "{}";
    public string? SenalIa { get; set; }
    public int? ConfianzaIa { get; set; }
    /// <summary>The probability (0-1) of the retrained model that the price is higher after its horizon; null if there was no model in force.</summary>
    public decimal? ProbabilidadMl { get; set; }
    /// <summary>"Compra" or "Venta" when the analysis ended in an order proposed to a person; null otherwise.</summary>
    public string? OrdenLado { get; set; }
    public DateTimeOffset? OrdenCreadaEn { get; set; }
    public DateTimeOffset CreadaEn { get; set; } = DateTimeOffset.UtcNow;

    // What the price did afterwards (null until it has been evaluated).
    public DateTimeOffset? EvaluadaEn { get; set; }
    public DateOnly? FechaEvaluacion { get; set; }
    public decimal? PrecioEvaluacionUsd { get; set; }
    public decimal? RetornoPorcentaje { get; set; }
    public bool? Acierto { get; set; }
}

/// <summary>
/// A version of the weights of the rules. "Aplicada" ones are history (the newest is the one in force); a "Propuesta" is what the learning suggests and waits
/// for a person; "Descartada" is a proposal someone turned down or that a newer one replaced.
/// </summary>
public sealed class CarteraPesos
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Version { get; set; }
    /// <summary>{"tendencia":1,"rsi":1,"macd":1,"bollinger":1}</summary>
    public string PesosJson { get; set; } = "{}";
    public string Estado { get; set; } = EstadoDePesos.Propuesta;
    public int Muestras { get; set; }
    public string? Motivo { get; set; }
    /// <summary>The numbers behind a proposal (per rule: samples, mean move in its direction, t statistic, weight before and after), as JSON.</summary>
    public string? DetalleJson { get; set; }
    public DateTimeOffset CreadaEn { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResueltaEn { get; set; }
    public Guid? ResueltaPor { get; set; }
}

public static class EstadoDePesos
{
    public const string Propuesta = "Propuesta";
    public const string Aplicada = "Aplicada";
    public const string Descartada = "Descartada";
}

/// <summary>
/// A version of the retrained model. Only an approved one becomes "Activo" (the one the analyses use); the one it replaces becomes "Anterior"; a candidate that
/// did not earn the place is kept as "Rechazado", with the reason, so there is a record of what was tried.
/// </summary>
public sealed class CarteraModelo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Version { get; set; }
    /// <summary>The model itself (features, standardisation, weights), as JSON.</summary>
    public string ModeloJson { get; set; } = "{}";
    public string Estado { get; set; } = EstadoDeModelo.Rechazado;
    public int MuestrasDeEntrenamiento { get; set; }
    public int MuestrasDePrueba { get; set; }
    public double Precision { get; set; }
    public double PrecisionBase { get; set; }
    public int Senales { get; set; }
    public double RetornoMedioConSenalPorcentaje { get; set; }
    public double RetornoMedioGeneralPorcentaje { get; set; }
    public string? Motivo { get; set; }
    public DateTimeOffset CreadoEn { get; set; } = DateTimeOffset.UtcNow;
}

public static class EstadoDeModelo
{
    public const string Activo = "Activo";
    public const string Anterior = "Anterior";
    public const string Rechazado = "Rechazado";
}
