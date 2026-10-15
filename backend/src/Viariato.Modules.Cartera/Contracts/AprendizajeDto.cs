namespace Viariato.Modules.Cartera.Contracts;

public sealed record RegistrarSenalRequest(
    string CasoId,
    string Ticker,
    DateOnly Fecha,
    decimal PrecioUsd,
    string Senal,
    decimal Puntos,
    string VotosJson,
    string? SenalIa,
    int? ConfianzaIa,
    string? OrdenLado,
    decimal? ProbabilidadMl = null);

public sealed record SenalRegistradaDto(Guid Id, bool YaEstaba);

public sealed record OrdenRecienteDto(string Ticker, string Lado, DateTimeOffset CreadaEn);

public sealed record SenalPorEvaluarDto(string CasoId, string Ticker, DateOnly Fecha, decimal PrecioUsd, string Senal);

public sealed record EvaluarSenalRequest(decimal PrecioUsd, DateOnly Fecha);

public sealed record SenalEvaluadaDto(string Ticker, string Senal, string VotosJson, decimal RetornoPorcentaje);

public sealed record PesosDto(int Version, IReadOnlyDictionary<string, double> Pesos);

public sealed record ProponerPesosRequest(IReadOnlyDictionary<string, double> Pesos, int Muestras, string? Motivo, string? DetalleJson);

public sealed record PropuestaDePesosDto(
    Guid Id,
    IReadOnlyDictionary<string, double> Pesos,
    int Muestras,
    string? Motivo,
    string? DetalleJson,
    DateTimeOffset CreadaEn);

public sealed record EstadisticaDto(string Senal, int Muestras, int Aciertos, double AciertoPorcentaje, double RetornoMedioPorcentaje);

public sealed record SenalRecienteDto(
    string Ticker,
    DateOnly Fecha,
    string Senal,
    decimal Puntos,
    string? SenalIa,
    int? ConfianzaIa,
    string? OrdenLado,
    decimal? RetornoPorcentaje,
    bool? Acierto,
    string CasoId,
    decimal? ProbabilidadMl = null);

/// <summary>A candidate model a robot trained, with how it did on days it had not seen and whether it earned the place of the one in force.</summary>
public sealed record EntregarModeloRequest(
    string ModeloJson,
    int MuestrasDeEntrenamiento,
    int MuestrasDePrueba,
    double Precision,
    double PrecisionBase,
    int Senales,
    double RetornoMedioConSenalPorcentaje,
    double RetornoMedioGeneralPorcentaje,
    bool Aprobado,
    string? Motivo);

public sealed record ModeloActivoDto(int Version, string ModeloJson);

public sealed record ModeloDto(
    Guid Id,
    int Version,
    string Estado,
    int MuestrasDeEntrenamiento,
    int MuestrasDePrueba,
    double Precision,
    double PrecisionBase,
    int Senales,
    double RetornoMedioConSenalPorcentaje,
    double RetornoMedioGeneralPorcentaje,
    string? Motivo,
    DateTimeOffset CreadoEn);

/// <summary>How the model in force has done on the analyses made since it was in force (days it could not have seen): a second test, in the real world.</summary>
public sealed record ModeloEnVivoDto(int Muestras, double AciertoPorcentaje, int Senales, double RetornoMedioConSenalPorcentaje);

/// <summary>What the trading robots have learned so far, for the screen.</summary>
public sealed record AprendizajeDto(
    int VersionDePesos,
    IReadOnlyDictionary<string, double> PesosActivos,
    PropuestaDePesosDto? Propuesta,
    int EvaluadasTotal,
    int PendientesDeEvaluar,
    IReadOnlyList<EstadisticaDto> Estadisticas,
    IReadOnlyList<SenalRecienteDto> Recientes,
    ModeloDto? ModeloActivo,
    ModeloEnVivoDto? ModeloEnVivo,
    IReadOnlyList<ModeloDto> Modelos);
