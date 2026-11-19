using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Despacho;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Lanzadores;

namespace Viariato.Modules.Casos.Familias;

/// <summary>
/// Ends the families whose moment has come, every few seconds: first it closes the open ones that reached their expected members, then
/// it ends the closed ones none of whose members is still going — running, waiting, or ended but with a reprocessing pending (it will
/// come back). Ending one fixes its summary, leaves «Terminada» in its history (what notification rules listen to) and, if its type says
/// so, opens its closing case.
/// </summary>
/// <remarks>
/// A worker rather than a reaction to each case's end: a family is only looked at here, once, whoever ended its last case and however
/// (a robot, a person cancelling, a reprocessing that ran out), and a turn missed because the API was down is made up on the next one.
/// Each turn is one transaction holding a Postgres advisory lock, like the launchers': with several API replicas only one ends families,
/// so a family's actions run once. Only open and closed families are looked at (an index on their state); the ended ones, which grow
/// for ever, never are.
/// </remarks>
public sealed class TerminadorDeFamilias(AppDbContext db, AbridorDeCasos abridor, ILogger<TerminadorDeFamilias> logger)
{
    private const long CerrojoDeFamilias = 7_204_611;
    private const int PorVuelta = 50;

    private static readonly CasoEstado[] EstadosFinales = [CasoEstado.Completado, CasoEstado.Fallido, CasoEstado.Cancelado];

    /// <summary>One turn. Public so it can be tested on its own. Returns how many families it ended.</summary>
    public async Task<int> UnaVueltaAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        var libre = await db.Database.SqlQuery<bool>($"select pg_try_advisory_xact_lock({CerrojoDeFamilias}) as \"Value\"").SingleAsync(ct);
        if (!libre) return 0;

        await CerrarLasCompletasAsync(ahora, ct);

        var listas = await db.Set<Familia>()
            .Include(f => f.Tipo)
            .Where(f => f.Estado == EstadoDeFamilia.Cerrada)
            .Where(f => !db.Set<MiembroDeFamilia>().Where(m => m.FamiliaId == f.Id)
                .Join(db.Set<Caso>(), m => m.CasoId, c => c.Id, (m, c) => c)
                .Any(c => !EstadosFinales.Contains(c.Estado)
                    || db.Set<ReprocesamientoProgramado>().Any(p => p.CasoId == c.Id && p.Estado == EstadoDeReprocesamiento.Pendiente)))
            .OrderBy(f => f.CerradaAt)
            .Take(PorVuelta)
            .ToListAsync(ct);

        foreach (var familia in listas)
        {
            await TerminarAsync(familia, ahora, ct);
        }

        await transaccion.CommitAsync(ct);
        return listas.Count;
    }

    /// <summary>Open families with as many members as they expected close by themselves.</summary>
    private async Task CerrarLasCompletasAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        var completas = await db.Set<Familia>()
            .Where(f => f.Estado == EstadoDeFamilia.Abierta && f.ParticipantesEsperados != null
                && db.Set<MiembroDeFamilia>().Count(m => m.FamiliaId == f.Id) >= f.ParticipantesEsperados)
            .Take(PorVuelta)
            .ToListAsync(ct);
        foreach (var familia in completas)
        {
            familia.Estado = EstadoDeFamilia.Cerrada;
            familia.CerradaAt = ahora;
            db.Add(new FamiliaEvento
            {
                FamiliaId = familia.Id,
                Accion = FamiliaEventoAccion.Cerrada,
                DetalleJson = JsonSerializer.Serialize(new { miembros = familia.ParticipantesEsperados, automatica = true }),
                OccurredAt = ahora,
            });
        }

        if (completas.Count > 0) await db.SaveChangesAsync(ct);
    }

    private async Task TerminarAsync(Familia familia, DateTimeOffset ahora, CancellationToken ct)
    {
        var casos = await db.Set<MiembroDeFamilia>().AsNoTracking()
            .Where(m => m.FamiliaId == familia.Id)
            .Join(db.Set<Caso>(), m => m.CasoId, c => c.Id, (m, c) => c)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new { c.Id, c.Titulo, c.Estado, EstadoNegocio = c.EstadoNegocioActual != null ? c.EstadoNegocioActual.Codigo : null })
            .ToListAsync(ct);

        familia.Estado = EstadoDeFamilia.Terminada;
        familia.TerminadaAt = ahora;
        familia.Completados = casos.Count(c => c.Estado == CasoEstado.Completado);
        familia.Fallidos = casos.Count(c => c.Estado == CasoEstado.Fallido);
        familia.Cancelados = casos.Count(c => c.Estado == CasoEstado.Cancelado);
        db.Add(new FamiliaEvento
        {
            FamiliaId = familia.Id,
            Accion = FamiliaEventoAccion.Terminada,
            DetalleJson = JsonSerializer.Serialize(new
            {
                total = casos.Count, completados = familia.Completados, fallidos = familia.Fallidos, cancelados = familia.Cancelados,
            }),
            OccurredAt = ahora,
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Familia «{Familia}» terminada: {Completados} completados, {Fallidos} fallidos, {Cancelados} cancelados.",
            familia.Nombre, familia.Completados, familia.Fallidos, familia.Cancelados);

        var tipo = familia.Tipo!;
        if (tipo.CreadorAlTerminarId is not { } creadorId) return;

        if (tipo.Condicion == CondicionDeCierre.SoloSiTodosBien && familia.Completados != casos.Count)
        {
            await AnotarCierreAsync(familia, null, "No todos los casos acabaron completados: el tipo de familia solo abre su caso de cierre si es así.", ahora, ct);
            return;
        }

        // The closing case gets the family as data: who it was and how each member ended. What each member's own data say, its robot asks
        // for (GET /api/v1/rpa/familias/{id}), so a big family does not make a huge case.
        var datos = JsonSerializer.Serialize(new
        {
            familia = new
            {
                id = familia.Id,
                nombre = familia.Nombre,
                tipo = tipo.Nombre,
                total = casos.Count,
                completados = familia.Completados,
                fallidos = familia.Fallidos,
                cancelados = familia.Cancelados,
                casos = casos.Select(c => new { id = c.Id, titulo = c.Titulo, estado = c.Estado.ToString(), estadoNegocio = c.EstadoNegocio }),
            },
        });

        AperturaDeCaso apertura;
        try
        {
            apertura = await abridor.AbrirAsync(creadorId, datos, new Dictionary<string, object?> { ["creadoPorFamiliaId"] = familia.Id }, ahora, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The family has ended whatever happens to its closing case: a failure is written down, not retried for ever.
            db.ChangeTracker.Clear();
            familia = await db.Set<Familia>().FirstAsync(f => f.Id == familia.Id, ct);
            apertura = new AperturaDeCaso(null, $"No se pudo abrir el caso de cierre: {ex.Message}");
        }

        await AnotarCierreAsync(familia, apertura.CasoId, apertura.Error, ahora, ct);
    }

    private async Task AnotarCierreAsync(Familia familia, Guid? casoId, string? nota, DateTimeOffset ahora, CancellationToken ct)
    {
        familia.CasoDeCierreId = casoId;
        familia.NotaDeCierre = nota is { Length: > 1000 } ? nota[..1000] : nota;
        db.Add(new FamiliaEvento
        {
            FamiliaId = familia.Id,
            Accion = casoId is null ? FamiliaEventoAccion.CierreNoAbierto : FamiliaEventoAccion.CierreAbierto,
            DetalleJson = JsonSerializer.Serialize(casoId is null ? (object)new { motivo = nota } : new { casoId, nota }),
            OccurredAt = ahora,
        });
        await db.SaveChangesAsync(ct);

        if (casoId is null) logger.LogWarning("Familia «{Familia}»: sin caso de cierre. {Motivo}", familia.Nombre, nota);
    }
}

/// <summary>The finisher's clock: every few seconds, a turn in a scope of its own.</summary>
internal sealed class VigilanteDeFamilias(IServiceScopeFactory scopes, IOptions<DespachoOptions> opciones, ILogger<VigilanteDeFamilias> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, opciones.Value.FamiliasSegundos)));
        try
        {
            while (await temporizador.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<TerminadorDeFamilias>().UnaVueltaAsync(DateTimeOffset.UtcNow, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A failed turn must not end the loop: the next one tries again.
                    logger.LogError(ex, "Falló la vuelta de las familias.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The API is shutting down.
        }
    }
}
