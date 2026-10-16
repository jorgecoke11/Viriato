using Viariato.Modules.Cartera.Contracts;
using Viariato.Modules.Cartera.Domain;

namespace Viariato.Modules.Cartera.Endpoints;

/// <summary>What the movements add up to: the cash and the positions, and their valuation. Pure functions, so they are tested without a database.</summary>
internal static class Calculo
{
    internal sealed record Posicion(string Ticker, decimal Cantidad, decimal CosteMedioUsd, decimal? StopUsd);

    /// <summary>Replays the movements in order. A purchase adds to the position at the average cost (and carries the newest stop); a sale takes from it.</summary>
    public static (decimal EfectivoEur, List<Posicion> Posiciones) Reproducir(decimal capitalInicialEur, IEnumerable<CarteraMovimiento> movimientos)
    {
        var efectivo = capitalInicialEur;
        var posiciones = new List<Posicion>();

        foreach (var m in movimientos)
        {
            var indice = posiciones.FindIndex(p => p.Ticker == m.Ticker);
            if (m.Lado == "Compra")
            {
                efectivo -= m.ImporteEur + m.ComisionEur;
                if (indice < 0)
                {
                    posiciones.Add(new Posicion(m.Ticker, m.Cantidad, m.PrecioUsd, m.StopUsd));
                }
                else
                {
                    var p = posiciones[indice];
                    var total = p.Cantidad + m.Cantidad;
                    posiciones[indice] = new Posicion(m.Ticker, total, (p.Cantidad * p.CosteMedioUsd + m.Cantidad * m.PrecioUsd) / total, m.StopUsd ?? p.StopUsd);
                }
            }
            else
            {
                efectivo += m.ImporteEur - m.ComisionEur;
                if (indice >= 0)
                {
                    var p = posiciones[indice];
                    var resto = p.Cantidad - m.Cantidad;
                    if (resto <= 0) posiciones.RemoveAt(indice);
                    else posiciones[indice] = p with { Cantidad = resto };
                }
            }
        }

        return (Math.Round(efectivo, 2), posiciones);
    }

    /// <summary>The portfolio valued at the last prices the robots saw (cost, if a value has none yet).</summary>
    public static CarteraDto Pantalla(decimal capitalInicialEur, IReadOnlyList<CarteraMovimiento> movimientos, IReadOnlyList<CarteraCotizacion> cotizaciones)
    {
        var (efectivo, posiciones) = Reproducir(capitalInicialEur, movimientos);

        // The euro-dollar rate of the freshest price; failing that, of the last movement; failing that, 1 (nothing is open then).
        var eurUsd = cotizaciones.OrderByDescending(c => c.UpdatedAt).Select(c => (decimal?)c.EurUsd).FirstOrDefault()
            ?? movimientos.OrderByDescending(m => m.Fecha).Select(m => (decimal?)m.EurUsd).FirstOrDefault()
            ?? 1m;

        var valoradas = posiciones.Select(p =>
        {
            var cotizacion = cotizaciones.FirstOrDefault(c => c.Ticker == p.Ticker);
            var precio = cotizacion?.PrecioUsd ?? p.CosteMedioUsd;
            var valor = Math.Round(p.Cantidad * precio / eurUsd, 2, MidpointRounding.AwayFromZero);
            var coste = Math.Round(p.Cantidad * p.CosteMedioUsd / eurUsd, 2, MidpointRounding.AwayFromZero);
            var ganancia = valor - coste;
            return new PosicionDto(
                p.Ticker, p.Cantidad, p.CosteMedioUsd, p.StopUsd, precio, cotizacion is null, cotizacion?.UpdatedAt,
                valor, ganancia, coste == 0 ? 0 : Math.Round(ganancia / coste * 100m, 2));
        }).OrderByDescending(p => p.ValorEur).ToList();

        var valorPosiciones = valoradas.Sum(p => p.ValorEur);
        var patrimonio = efectivo + valorPosiciones;
        var rentabilidad = patrimonio - capitalInicialEur;

        return new CarteraDto(
            capitalInicialEur, efectivo, valorPosiciones, patrimonio, rentabilidad,
            capitalInicialEur == 0 ? 0 : Math.Round(rentabilidad / capitalInicialEur * 100m, 2),
            eurUsd, valoradas,
            movimientos.OrderByDescending(m => m.Fecha).Take(200).Select(Mapear).ToList());
    }

    /// <summary>Was the signal right? Buy: the price rose. Sell: it fell. Hold: it stayed within <paramref name="banda"/> percent. (The robots judge with the same rule.)</summary>
    public static bool Acierto(string senal, decimal retornoPorcentaje, decimal banda) => senal switch
    {
        "Comprar" => retornoPorcentaje > 0,
        "Vender" => retornoPorcentaje < 0,
        _ => Math.Abs(retornoPorcentaje) < banda,
    };

    public static MovimientoDto Mapear(CarteraMovimiento m) =>
        new(m.Id, m.CasoId, m.Ticker, m.Lado, m.Cantidad, m.PrecioUsd, m.EurUsd, m.ImporteEur, m.ComisionEur, m.EfectivoTrasEur, m.StopUsd, m.Fecha);
}
