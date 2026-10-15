namespace Viariato.Modules.Cartera.Contracts;

/// <summary>An open position, as the robots need it (cost and stop) — no valuation.</summary>
public sealed record PosicionRobotDto(string Ticker, decimal Cantidad, decimal CosteMedioUsd, decimal? StopUsd);

/// <summary>The portfolio as a robot reads it before deciding or executing.</summary>
public sealed record CarteraRobotDto(decimal CapitalInicialEur, decimal EfectivoEur, IReadOnlyList<PosicionRobotDto> Posiciones);

public sealed record RegistrarMovimientoRequest(
    string CasoId,
    string Ticker,
    string Lado,
    decimal Cantidad,
    decimal PrecioUsd,
    decimal EurUsd,
    decimal ComisionPorcentaje,
    decimal? StopUsd,
    decimal CapitalInicialEur);

public sealed record MovimientoDto(
    Guid Id,
    string CasoId,
    string Ticker,
    string Lado,
    decimal Cantidad,
    decimal PrecioUsd,
    decimal EurUsd,
    decimal ImporteEur,
    decimal ComisionEur,
    decimal EfectivoTrasEur,
    decimal? StopUsd,
    DateTimeOffset Fecha);

/// <summary>What registering a movement answered: the movement, and whether it was already registered (the same case executed again).</summary>
public sealed record MovimientoRegistradoDto(MovimientoDto Movimiento, bool YaEstaba);

public sealed record GuardarCotizacionRequest(decimal PrecioUsd, decimal EurUsd);

/// <summary>An open position valued at the last price the robots saw. Without a price yet it is valued at its cost and says so.</summary>
public sealed record PosicionDto(
    string Ticker,
    decimal Cantidad,
    decimal CosteMedioUsd,
    decimal? StopUsd,
    decimal PrecioActualUsd,
    bool PrecioEsElCoste,
    DateTimeOffset? PrecioDe,
    decimal ValorEur,
    decimal GananciaEur,
    decimal GananciaPorcentaje);

/// <summary>The portfolio as the screen shows it.</summary>
public sealed record CarteraDto(
    decimal CapitalInicialEur,
    decimal EfectivoEur,
    decimal ValorPosicionesEur,
    decimal PatrimonioEur,
    decimal RentabilidadEur,
    decimal RentabilidadPorcentaje,
    decimal EurUsd,
    IReadOnlyList<PosicionDto> Posiciones,
    IReadOnlyList<MovimientoDto> Movimientos);
