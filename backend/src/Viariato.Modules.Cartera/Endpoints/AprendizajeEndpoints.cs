using System.Text.Json;
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
/// What the trading robots learn from. They register each analysis (what the rules said and how each rule voted), later compare it with what the price
/// did, and propose small changes to the weights of the rules; a person looks at the numbers and applies or discards the change. Nothing here trades.
/// </summary>
internal static partial class AprendizajeEndpoints
{
    private static readonly string[] Reglas = ["tendencia", "rsi", "macd", "bollinger"];
    private const double PesoMinimo = 0.67;
    private const double PesoMaximo = 1.5;
    /// <summary>A "hold" is right if the price stayed within this many percent of where it was (the same number the robots use).</summary>
    private const decimal BandaDeMantener = 2.0m;

    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9.\-]{0,11}$")]
    private static partial Regex PatronDeTicker();

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var persona = endpoints.MapGroup("/api/v1/cartera/aprendizaje").RequireAuthorization(Permissions.CarteraRead);
        persona.MapGet("/", LeerAsync);
        endpoints.MapPost("/api/v1/cartera/aprendizaje/propuesta/{id:guid}/aplicar", AplicarAsync).RequireAuthorization(Permissions.CarteraGestionar);
        endpoints.MapPost("/api/v1/cartera/aprendizaje/propuesta/{id:guid}/descartar", DescartarAsync).RequireAuthorization(Permissions.CarteraGestionar);

        var robot = endpoints.MapGroup("/api/v1/rpa/cartera")
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(ApiKeyDefaults.AuthenticationScheme)
                .RequireClaim(ApiKeyDefaults.DespliegueIdClaimType));
        robot.MapPost("/senales", RegistrarAsync);
        robot.MapGet("/senales/por-evaluar", PorEvaluarAsync);
        robot.MapPut("/senales/{casoId}/evaluacion", EvaluarAsync);
        robot.MapGet("/senales/evaluadas", EvaluadasAsync);
        robot.MapGet("/ordenes-recientes", OrdenesRecientesAsync);
        robot.MapGet("/pesos", PesosActivosAsync);
        robot.MapPost("/pesos/propuesta", ProponerAsync);
        robot.MapGet("/modelos/activo", ModeloActivoAsync);
        robot.MapPost("/modelos", EntregarModeloAsync);
    }

    // ---------------------------------------------------------------- the robots

    private static async Task<IResult> RegistrarAsync(RegistrarSenalRequest r, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var ticker = r.Ticker?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(r.CasoId) || r.CasoId.Length > 64) return Problema(http, "La señal necesita el id del caso (máximo 64 caracteres).");
        if (!PatronDeTicker().IsMatch(ticker)) return Problema(http, "El símbolo del valor no es válido.");
        if (r.Senal is not ("Comprar" or "Vender" or "Mantener")) return Problema(http, "La señal debe ser Comprar, Vender o Mantener.");
        if (r.PrecioUsd <= 0) return Problema(http, "El precio debe ser mayor que cero.");
        if (r.OrdenLado is not (null or "Compra" or "Venta")) return Problema(http, "El lado de la orden debe ser Compra o Venta.");
        if (!VotosValidos(r.VotosJson)) return Problema(http, "Los votos deben ser un objeto con un número por regla (tendencia, rsi, macd, bollinger).");

        var previa = await db.Set<CarteraSenal>().AsNoTracking().FirstOrDefaultAsync(s => s.CasoId == r.CasoId, ct);
        if (previa is not null) return Results.Ok(new SenalRegistradaDto(previa.Id, true));

        var senal = new CarteraSenal
        {
            CasoId = r.CasoId,
            Ticker = ticker,
            Fecha = r.Fecha,
            PrecioUsd = r.PrecioUsd,
            Senal = r.Senal,
            Puntos = r.Puntos,
            VotosJson = r.VotosJson,
            SenalIa = r.SenalIa?.Trim().ToLowerInvariant() is { Length: > 0 and <= 10 } ia ? ia : null,
            ProbabilidadMl = r.ProbabilidadMl is >= 0 and <= 1 ? r.ProbabilidadMl : null,
            ConfianzaIa = r.ConfianzaIa is >= 0 and <= 100 ? r.ConfianzaIa : null,
            OrdenLado = r.OrdenLado,
            OrdenCreadaEn = r.OrdenLado is null ? null : DateTimeOffset.UtcNow,
        };
        db.Add(senal);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            var ganadora = await db.Set<CarteraSenal>().AsNoTracking().FirstOrDefaultAsync(s => s.CasoId == r.CasoId, ct);
            if (ganadora is not null) return Results.Ok(new SenalRegistradaDto(ganadora.Id, true));
            throw;
        }

        return Results.Ok(new SenalRegistradaDto(senal.Id, false));
    }

    private static async Task<IResult> OrdenesRecientesAsync(int? horas, AppDbContext db, CancellationToken ct)
    {
        var desde = DateTimeOffset.UtcNow.AddHours(-Math.Clamp(horas ?? 24, 1, 24 * 14));
        var ordenes = await db.Set<CarteraSenal>().AsNoTracking()
            .Where(s => s.OrdenLado != null && s.OrdenCreadaEn >= desde)
            .OrderByDescending(s => s.OrdenCreadaEn)
            .Select(s => new OrdenRecienteDto(s.Ticker, s.OrdenLado!, s.OrdenCreadaEn!.Value))
            .ToListAsync(ct);
        return Results.Ok(ordenes);
    }

    private static async Task<IResult> PorEvaluarAsync(int? dias, int? limite, AppDbContext db, CancellationToken ct)
    {
        var hasta = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-Math.Clamp(dias ?? 5, 1, 60));
        var senales = await db.Set<CarteraSenal>().AsNoTracking()
            .Where(s => s.EvaluadaEn == null && s.Fecha <= hasta)
            .OrderBy(s => s.Fecha)
            .Take(Math.Clamp(limite ?? 200, 1, 1000))
            .Select(s => new SenalPorEvaluarDto(s.CasoId, s.Ticker, s.Fecha, s.PrecioUsd, s.Senal))
            .ToListAsync(ct);
        return Results.Ok(senales);
    }

    private static async Task<IResult> EvaluarAsync(string casoId, EvaluarSenalRequest r, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (r.PrecioUsd <= 0) return Problema(http, "El precio debe ser mayor que cero.");
        var senal = await db.Set<CarteraSenal>().FirstOrDefaultAsync(s => s.CasoId == casoId, ct);
        if (senal is null) return ProblemResults.NotFound(http, "Señal no encontrada.");
        if (senal.EvaluadaEn is not null) return Results.NoContent(); // already judged: the first judgement stands

        var retorno = Math.Round((r.PrecioUsd - senal.PrecioUsd) / senal.PrecioUsd * 100m, 4);
        senal.EvaluadaEn = DateTimeOffset.UtcNow;
        senal.FechaEvaluacion = r.Fecha;
        senal.PrecioEvaluacionUsd = r.PrecioUsd;
        senal.RetornoPorcentaje = retorno;
        senal.Acierto = Calculo.Acierto(senal.Senal, retorno, BandaDeMantener);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> EvaluadasAsync(int? limite, AppDbContext db, CancellationToken ct)
    {
        var evaluadas = await db.Set<CarteraSenal>().AsNoTracking()
            .Where(s => s.EvaluadaEn != null)
            .OrderByDescending(s => s.EvaluadaEn)
            .Take(Math.Clamp(limite ?? 2000, 1, 5000))
            .Select(s => new SenalEvaluadaDto(s.Ticker, s.Senal, s.VotosJson, s.RetornoPorcentaje!.Value))
            .ToListAsync(ct);
        return Results.Ok(evaluadas);
    }

    private static async Task<IResult> PesosActivosAsync(AppDbContext db, CancellationToken ct)
    {
        var (version, pesos) = await PesosEnVigorAsync(db, ct);
        return Results.Ok(new PesosDto(version, pesos));
    }

    private static async Task<IResult> ProponerAsync(ProponerPesosRequest r, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (!PesosValidos(r.Pesos)) return Problema(http, $"Los pesos deben ser un número por regla ({string.Join(", ", Reglas)}) entre {PesoMinimo} y {PesoMaximo}.");
        if (r.Muestras < 1) return Problema(http, "Una propuesta necesita al menos una muestra.");

        // A new proposal replaces the one still waiting: a person only ever has one to look at.
        foreach (var vieja in await db.Set<CarteraPesos>().Where(p => p.Estado == EstadoDePesos.Propuesta).ToListAsync(ct))
        {
            vieja.Estado = EstadoDePesos.Descartada;
            vieja.ResueltaEn = DateTimeOffset.UtcNow;
        }

        var version = (await db.Set<CarteraPesos>().MaxAsync(p => (int?)p.Version, ct) ?? 0) + 1;
        db.Add(new CarteraPesos
        {
            Version = version,
            PesosJson = JsonSerializer.Serialize(r.Pesos),
            Estado = EstadoDePesos.Propuesta,
            Muestras = r.Muestras,
            Motivo = r.Motivo is { Length: > 1000 } ? r.Motivo[..1000] : r.Motivo,
            DetalleJson = r.DetalleJson is { Length: > 4000 } ? null : r.DetalleJson,
        });
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ModeloActivoAsync(AppDbContext db, CancellationToken ct)
    {
        var activo = await db.Set<CarteraModelo>().AsNoTracking().FirstOrDefaultAsync(m => m.Estado == EstadoDeModelo.Activo, ct);
        return activo is null ? Results.NoContent() : Results.Ok(new ModeloActivoDto(activo.Version, activo.ModeloJson));
    }

    private static async Task<IResult> EntregarModeloAsync(EntregarModeloRequest r, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.ModeloJson) || r.ModeloJson.Length > 8000) return Problema(http, "El modelo debe ser un JSON de hasta 8000 caracteres.");
        try
        {
            using var _ = JsonDocument.Parse(r.ModeloJson);
        }
        catch (JsonException)
        {
            return Problema(http, "El modelo no es un JSON válido.");
        }

        var version = (await db.Set<CarteraModelo>().MaxAsync(m => (int?)m.Version, ct) ?? 0) + 1;
        // Only an approved model takes the place of the one in force; the one it replaces is kept as "Anterior". A refused one is kept to say what was tried.
        if (r.Aprobado)
        {
            foreach (var viejo in await db.Set<CarteraModelo>().Where(m => m.Estado == EstadoDeModelo.Activo).ToListAsync(ct)) viejo.Estado = EstadoDeModelo.Anterior;
        }

        db.Add(new CarteraModelo
        {
            Version = version,
            ModeloJson = r.ModeloJson,
            Estado = r.Aprobado ? EstadoDeModelo.Activo : EstadoDeModelo.Rechazado,
            MuestrasDeEntrenamiento = r.MuestrasDeEntrenamiento,
            MuestrasDePrueba = r.MuestrasDePrueba,
            Precision = r.Precision,
            PrecisionBase = r.PrecisionBase,
            Senales = r.Senales,
            RetornoMedioConSenalPorcentaje = r.RetornoMedioConSenalPorcentaje,
            RetornoMedioGeneralPorcentaje = r.RetornoMedioGeneralPorcentaje,
            Motivo = r.Motivo is { Length: > 1000 } ? r.Motivo[..1000] : r.Motivo,
        });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { version, activo = r.Aprobado });
    }

    // ---------------------------------------------------------------- what people see and decide

    private static async Task<IResult> LeerAsync(AppDbContext db, CancellationToken ct)
    {
        var (version, pesos) = await PesosEnVigorAsync(db, ct);
        var propuesta = await db.Set<CarteraPesos>().AsNoTracking().Where(p => p.Estado == EstadoDePesos.Propuesta).OrderByDescending(p => p.Version).FirstOrDefaultAsync(ct);

        var evaluadas = await db.Set<CarteraSenal>().AsNoTracking()
            .Where(s => s.EvaluadaEn != null)
            .Select(s => new { s.Senal, Retorno = s.RetornoPorcentaje!.Value, Acierto = s.Acierto == true })
            .ToListAsync(ct);
        var estadisticas = evaluadas
            .GroupBy(e => e.Senal)
            .OrderBy(g => g.Key)
            .Select(g => new EstadisticaDto(
                g.Key, g.Count(), g.Count(e => e.Acierto), Math.Round(g.Count(e => e.Acierto) * 100.0 / g.Count(), 1), Math.Round((double)g.Average(e => e.Retorno), 3)))
            .ToList();

        var hasta = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);
        var pendientes = await db.Set<CarteraSenal>().CountAsync(s => s.EvaluadaEn == null && s.Fecha <= hasta, ct);
        var recientes = await db.Set<CarteraSenal>().AsNoTracking()
            .OrderByDescending(s => s.CreadaEn).Take(50)
            .Select(s => new SenalRecienteDto(s.Ticker, s.Fecha, s.Senal, s.Puntos, s.SenalIa, s.ConfianzaIa, s.OrdenLado, s.RetornoPorcentaje, s.Acierto, s.CasoId, s.ProbabilidadMl))
            .ToListAsync(ct);

        var modelos = await db.Set<CarteraModelo>().AsNoTracking().OrderByDescending(m => m.Version).Take(10).ToListAsync(ct);
        var activo = modelos.FirstOrDefault(m => m.Estado == EstadoDeModelo.Activo) ?? await db.Set<CarteraModelo>().AsNoTracking().FirstOrDefaultAsync(m => m.Estado == EstadoDeModelo.Activo, ct);
        ModeloEnVivoDto? enVivo = null;
        if (activo is not null)
        {
            // Judged on the analyses made once it was in force: days it could not have been trained on.
            var juzgadas = await db.Set<CarteraSenal>().AsNoTracking()
                .Where(s => s.EvaluadaEn != null && s.ProbabilidadMl != null && s.CreadaEn >= activo.CreadoEn)
                .Select(s => new { P = s.ProbabilidadMl!.Value, Retorno = s.RetornoPorcentaje!.Value })
                .ToListAsync(ct);
            if (juzgadas.Count > 0)
            {
                var conSenal = juzgadas.Where(j => j.P >= 0.55m).ToList();
                enVivo = new ModeloEnVivoDto(
                    juzgadas.Count,
                    Math.Round(juzgadas.Count(j => (j.P >= 0.5m) == (j.Retorno > 0)) * 100.0 / juzgadas.Count, 1),
                    conSenal.Count,
                    conSenal.Count == 0 ? 0 : Math.Round((double)conSenal.Average(j => j.Retorno), 3));
            }
        }

        ModeloDto Mapear(CarteraModelo m) => new(
            m.Id, m.Version, m.Estado, m.MuestrasDeEntrenamiento, m.MuestrasDePrueba, m.Precision, m.PrecisionBase, m.Senales,
            m.RetornoMedioConSenalPorcentaje, m.RetornoMedioGeneralPorcentaje, m.Motivo, m.CreadoEn);

        return Results.Ok(new AprendizajeDto(
            version, pesos,
            propuesta is null ? null : new PropuestaDePesosDto(propuesta.Id, LeerPesos(propuesta.PesosJson), propuesta.Muestras, propuesta.Motivo, propuesta.DetalleJson, propuesta.CreadaEn),
            evaluadas.Count, pendientes, estadisticas, recientes,
            activo is null ? null : Mapear(activo), enVivo, modelos.Select(Mapear).ToList()));
    }

    private static async Task<IResult> AplicarAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var propuesta = await db.Set<CarteraPesos>().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (propuesta is null) return ProblemResults.NotFound(http, "Propuesta no encontrada.");
        if (propuesta.Estado != EstadoDePesos.Propuesta) return ProblemResults.Conflict(http, "Esta propuesta ya se resolvió.");

        propuesta.Estado = EstadoDePesos.Aplicada;
        propuesta.ResueltaEn = DateTimeOffset.UtcNow;
        propuesta.ResueltaPor = http.User.GetUserId();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> DescartarAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var propuesta = await db.Set<CarteraPesos>().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (propuesta is null) return ProblemResults.NotFound(http, "Propuesta no encontrada.");
        if (propuesta.Estado != EstadoDePesos.Propuesta) return ProblemResults.Conflict(http, "Esta propuesta ya se resolvió.");

        propuesta.Estado = EstadoDePesos.Descartada;
        propuesta.ResueltaEn = DateTimeOffset.UtcNow;
        propuesta.ResueltaPor = http.User.GetUserId();
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>The weights in force: the newest applied version, or 1 for every rule while nobody has applied any.</summary>
    private static async Task<(int Version, IReadOnlyDictionary<string, double> Pesos)> PesosEnVigorAsync(AppDbContext db, CancellationToken ct)
    {
        var aplicada = await db.Set<CarteraPesos>().AsNoTracking().Where(p => p.Estado == EstadoDePesos.Aplicada).OrderByDescending(p => p.Version).FirstOrDefaultAsync(ct);
        return aplicada is null ? (0, Reglas.ToDictionary(r => r, _ => 1.0)) : (aplicada.Version, LeerPesos(aplicada.PesosJson));
    }

    private static IReadOnlyDictionary<string, double> LeerPesos(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, double>>(json) ?? Reglas.ToDictionary(r => r, _ => 1.0);

    private static bool PesosValidos(IReadOnlyDictionary<string, double>? pesos) =>
        pesos is not null && pesos.Count == Reglas.Length && Reglas.All(r => pesos.TryGetValue(r, out var p) && p is >= PesoMinimo - 1e-9 and <= PesoMaximo + 1e-9);

    private static bool VotosValidos(string json)
    {
        try
        {
            var votos = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
            return votos is not null && votos.Count == Reglas.Length && Reglas.All(r => votos.TryGetValue(r, out var v) && v is >= -2 and <= 2);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IResult Problema(HttpContext http, string detalle) => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest, title: "Petición no válida", detail: detalle, instance: http.Request.Path);
}
