using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Despacho;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Flujos.Esquemas;

namespace Viariato.Modules.Casos.Lanzadores;

/// <summary>
/// The crontab of the platform: every few seconds it looks at the launchers of the processes and, for each one whose moment has come,
/// opens the Caso it describes. What it has fired is written on the launcher itself (the moment, not the clock at which it noticed), so a
/// restart never repeats a moment and, with several API replicas, only one of them looks at a time. If the platform was down through a moment, it is
/// skipped once the launcher's tolerance has gone by instead of being fired late on stale ground.
/// </summary>
public sealed class VigilanteDeLanzadores(IServiceScopeFactory scopes, IOptions<DespachoOptions> opciones, ILogger<VigilanteDeLanzadores> logger) : BackgroundService
{
    /// <summary>Any value will do: it only has to be the same everywhere, and not another sweep's.</summary>
    private const long CerrojoDeLanzadores = 7_204_603;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, opciones.Value.LanzadoresSegundos)));

        try
        {
            while (await temporizador.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await UnaVueltaAsync(DateTimeOffset.UtcNow, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A failed turn must not end the loop: the next one tries again.
                    logger.LogError(ex, "Falló la vuelta de los lanzadores.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The API is shutting down.
        }
    }

    /// <summary>One look at the clock. Public so it can be tested with a clock of its own.</summary>
    public async Task<int> UnaVueltaAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        // A scope per turn: the work goes through the database context, which must not live for ever.
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var libre = await db.Database.SqlQuery<bool>($"select pg_try_advisory_xact_lock({CerrojoDeLanzadores}) as \"Value\"").SingleAsync(ct);
        if (!libre) return 0;

        var abridor = scope.ServiceProvider.GetRequiredService<AbridorDeCasos>();
        var lanzadores = await db.Set<LanzadorDeCaso>().Where(l => l.Activo).OrderBy(l => l.Nombre).ToListAsync(ct);

        var abiertos = 0;
        foreach (var lanzador in lanzadores)
        {
            var zona = CronDeLanzador.BuscarZona(lanzador.ZonaHoraria);
            if (zona is null || !CronDeLanzador.TryLeer(lanzador.Cron, out var expresiones, out _)) continue; // it was valid when saved; a machine without that zone just waits

            var momento = CronDeLanzador.Pendiente(ahora, zona, expresiones, lanzador.UltimoDisparoEn, TimeSpan.FromMinutes(lanzador.ToleranciaMinutos));
            if (momento is not { } toca) continue;

            var cuando = TimeZoneInfo.ConvertTime(toca, zona);
            var casosAbiertos = 0;
            Guid? ultimoCaso = null;
            var errores = new List<string>();
            if (!DatosDeLanzador.TryElementos(lanzador.DatosJson, out var elementos, out var errorDeDatos))
            {
                errores.Add(errorDeDatos!);
            }
            else
            {
                foreach (var datos in elementos)
                {
                    var intento = await abridor.AbrirAsync(lanzador.CreadorDeCasoId, datos, lanzador.Id, cuando, ct);
                    if (intento.CasoId is { } abierto)
                    {
                        casosAbiertos++;
                        ultimoCaso = abierto;
                    }

                    if (intento.Error is not null && !errores.Contains(intento.Error)) errores.Add(intento.Error);
                }
            }

            var error = errores.Count == 0
                ? null
                : (elementos.Count > 1 ? $"Se abrieron {casosAbiertos} de {elementos.Count} casos. " : string.Empty) + string.Join(" ", errores);
            var apertura = new AperturaDeCaso(ultimoCaso, error);

            // The moment is marked as fired whatever came of it: an error is shown on the launcher and it tries again at the next moment,
            // not every few seconds for as long as the tolerance lasts.
            lanzador.UltimoDisparoEn = toca;
            lanzador.UltimoCasoId = apertura.CasoId ?? lanzador.UltimoCasoId;
            lanzador.UltimoError = apertura.Error is null ? null : (apertura.Error.Length > 1000 ? apertura.Error[..1000] : apertura.Error);
            lanzador.UpdatedAt = ahora;
            await db.SaveChangesAsync(ct);

            abiertos += casosAbiertos;
            if (apertura.Error is null)
            {
                logger.LogInformation("El lanzador «{Lanzador}» abrió {Casos} caso(s) (momento {Momento:O}).", lanzador.Nombre, casosAbiertos, toca);
            }
            else
            {
                logger.LogWarning("El lanzador «{Lanzador}» no pudo abrir todo lo que debía: {Error}", lanzador.Nombre, apertura.Error);
            }
        }

        await transaccion.CommitAsync(ct);
        return abiertos;
    }
}
