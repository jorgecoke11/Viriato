using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Cartera.Contracts;
using Viariato.Modules.Cartera.Domain;
using Viariato.Modules.RpaFleet.Auth;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Cartera.Endpoints;

/// <summary>
/// The simulated portfolio of the trading robots, kept in Viriato so it can be seen on screen. The robots read it and write to it with their key;
/// people read it with <c>cartera.read</c>. Nothing here is real money and nothing talks to a broker. Every robot key can write to it for now: the
/// platform has no notion yet of which service may touch which data.
/// </summary>
public static partial class CarteraEndpoints
{
    // A US ticker: letters, digits, dot or dash (BRK.B, BF-B).
    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9.\-]{0,11}$")]
    private static partial Regex PatronDeTicker();

    public static void MapCarteraEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var persona = endpoints.MapGroup("/api/v1/cartera").RequireAuthorization(Permissions.CarteraRead);
        persona.MapGet("/", LeerAsync);

        var robot = endpoints.MapGroup("/api/v1/rpa/cartera")
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(ApiKeyDefaults.AuthenticationScheme)
                .RequireClaim(ApiKeyDefaults.DespliegueIdClaimType));
        robot.MapGet("/", LeerParaRobotAsync);
        robot.MapPost("/movimientos", RegistrarAsync);
        robot.MapPut("/cotizaciones/{ticker}", GuardarCotizacionAsync);

        AprendizajeEndpoints.Map(endpoints);
    }

    // ---- what people see

    private static async Task<IResult> LeerAsync(AppDbContext db, CancellationToken ct)
    {
        var estado = await db.Set<CarteraEstado>().AsNoTracking().FirstOrDefaultAsync(ct);
        var movimientos = await db.Set<CarteraMovimiento>().AsNoTracking().OrderBy(m => m.Fecha).ThenBy(m => m.Id).ToListAsync(ct);
        var cotizaciones = await db.Set<CarteraCotizacion>().AsNoTracking().ToListAsync(ct);
        return Results.Ok(Calculo.Pantalla(estado?.CapitalInicialEur ?? 0m, movimientos, cotizaciones));
    }

    // ---- what the robots do

    private static async Task<IResult> LeerParaRobotAsync(decimal? capitalInicialEur, AppDbContext db, CancellationToken ct)
    {
        var estado = await ObtenerEstadoAsync(db, capitalInicialEur, ct);
        var movimientos = await db.Set<CarteraMovimiento>().AsNoTracking().OrderBy(m => m.Fecha).ThenBy(m => m.Id).ToListAsync(ct);
        var (efectivo, posiciones) = Calculo.Reproducir(estado.CapitalInicialEur, movimientos);
        return Results.Ok(new CarteraRobotDto(
            estado.CapitalInicialEur, efectivo,
            posiciones.Select(p => new PosicionRobotDto(p.Ticker, p.Cantidad, p.CosteMedioUsd, p.StopUsd)).ToList()));
    }

    private static async Task<IResult> RegistrarAsync(RegistrarMovimientoRequest r, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var ticker = r.Ticker?.Trim().ToUpperInvariant() ?? string.Empty;
        var lado = r.Lado?.Trim();
        if (string.IsNullOrWhiteSpace(r.CasoId) || r.CasoId.Length > 64) return Problema(http, "El movimiento necesita el id del caso (máximo 64 caracteres).");
        if (!PatronDeTicker().IsMatch(ticker)) return Problema(http, "El símbolo del valor no es válido.");
        if (lado is not ("Compra" or "Venta")) return Problema(http, "El lado debe ser Compra o Venta.");
        if (r.Cantidad <= 0 || r.PrecioUsd <= 0 || r.EurUsd <= 0) return Problema(http, "La cantidad, el precio y el tipo de cambio deben ser mayores que cero.");
        if (r.ComisionPorcentaje is < 0 or > 10) return Problema(http, "La comisión debe estar entre 0 y 10 %.");

        var previo = await db.Set<CarteraMovimiento>().AsNoTracking().FirstOrDefaultAsync(m => m.CasoId == r.CasoId, ct);
        if (previo is not null) return Results.Ok(new MovimientoRegistradoDto(Calculo.Mapear(previo), true));

        var estado = await ObtenerEstadoAsync(db, r.CapitalInicialEur, ct);
        var movimientos = await db.Set<CarteraMovimiento>().AsNoTracking().OrderBy(m => m.Fecha).ThenBy(m => m.Id).ToListAsync(ct);
        var (efectivo, posiciones) = Calculo.Reproducir(estado.CapitalInicialEur, movimientos);

        var importe = Math.Round(r.Cantidad * r.PrecioUsd / r.EurUsd, 2, MidpointRounding.AwayFromZero);
        var comision = Math.Round(importe * r.ComisionPorcentaje / 100m, 2, MidpointRounding.AwayFromZero);
        var posicion = posiciones.FirstOrDefault(p => p.Ticker == ticker);

        decimal efectivoTras;
        if (lado == "Compra")
        {
            if (importe + comision > efectivo)
            {
                return ProblemResults.Conflict(http, $"No hay efectivo: la compra cuesta {importe + comision:F2} € y hay {efectivo:F2} €.");
            }

            efectivoTras = efectivo - importe - comision;
        }
        else
        {
            if (posicion is null || posicion.Cantidad < r.Cantidad)
            {
                return ProblemResults.Conflict(http, $"No se puede vender {r.Cantidad} de {ticker}: en cartera hay {posicion?.Cantidad ?? 0}.");
            }

            efectivoTras = efectivo + importe - comision;
        }

        var movimiento = new CarteraMovimiento
        {
            CasoId = r.CasoId,
            Ticker = ticker,
            Lado = lado,
            Cantidad = r.Cantidad,
            PrecioUsd = r.PrecioUsd,
            EurUsd = r.EurUsd,
            ImporteEur = importe,
            ComisionEur = comision,
            EfectivoTrasEur = efectivoTras,
            StopUsd = lado == "Compra" ? r.StopUsd : null,
            Fecha = DateTimeOffset.UtcNow,
        };
        db.Add(movimiento);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Two executions of the same case at once: the second one finds the first one's movement.
            var ganador = await db.Set<CarteraMovimiento>().AsNoTracking().FirstOrDefaultAsync(m => m.CasoId == r.CasoId, ct);
            if (ganador is not null) return Results.Ok(new MovimientoRegistradoDto(Calculo.Mapear(ganador), true));
            throw;
        }

        return Results.Ok(new MovimientoRegistradoDto(Calculo.Mapear(movimiento), false));
    }

    private static async Task<IResult> GuardarCotizacionAsync(string ticker, GuardarCotizacionRequest r, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var simbolo = ticker.Trim().ToUpperInvariant();
        if (!PatronDeTicker().IsMatch(simbolo)) return Problema(http, "El símbolo del valor no es válido.");
        if (r.PrecioUsd <= 0 || r.EurUsd <= 0) return Problema(http, "El precio y el tipo de cambio deben ser mayores que cero.");

        var cotizacion = await db.Set<CarteraCotizacion>().FirstOrDefaultAsync(c => c.Ticker == simbolo, ct);
        if (cotizacion is null)
        {
            db.Add(new CarteraCotizacion { Ticker = simbolo, PrecioUsd = r.PrecioUsd, EurUsd = r.EurUsd, UpdatedAt = DateTimeOffset.UtcNow });
        }
        else
        {
            cotizacion.PrecioUsd = r.PrecioUsd;
            cotizacion.EurUsd = r.EurUsd;
            cotizacion.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>The portfolio's starting capital, created the first time a robot asks (with the capital its process says). It never changes afterwards.</summary>
    private static async Task<CarteraEstado> ObtenerEstadoAsync(AppDbContext db, decimal? capitalInicialEur, CancellationToken ct)
    {
        var estado = await db.Set<CarteraEstado>().FirstOrDefaultAsync(ct);
        if (estado is not null) return estado;

        estado = new CarteraEstado { CapitalInicialEur = capitalInicialEur is > 0 ? capitalInicialEur.Value : 100m };
        db.Add(estado);
        await db.SaveChangesAsync(ct);
        return estado;
    }

    private static IResult Problema(HttpContext http, string detalle) => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest, title: "Petición no válida", detail: detalle, instance: http.Request.Path);
}
