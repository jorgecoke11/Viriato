using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Orchestration;
using Viariato.Modules.Casos.Orchestration.Ejecutores;
using Viariato.Modules.Flujos.Domain;

namespace Viariato.Modules.Casos.Tests.Orchestration;

public sealed class EjecucionOrchestratorTests
{
    // Only the synchronous executors (Interno/Decision/RevisionHumana) are registered — they don't
    // touch ITrabajoTracker/IBackgroundTaskQueue, so these tests exercise the orchestrator's own
    // control flow without needing the background queue machinery.
    private static IServiceProvider BuildExecutorProvider()
    {
        var services = new ServiceCollection();
        services.AddKeyedTransient<IPasoEjecutor, InternoPasoEjecutor>(TipoPaso.Interno);
        services.AddKeyedTransient<IPasoEjecutor, DecisionPasoEjecutor>(TipoPaso.Decision);
        services.AddKeyedTransient<IPasoEjecutor, RevisionHumanaPasoEjecutor>(TipoPaso.RevisionHumana);
        return services.BuildServiceProvider();
    }

    private static async Task<Caso> CrearCasoAsync(AppDbContext db, string? datosJson, params (int Orden, string Nombre, TipoPaso Tipo, string? Configuracion)[] pasos)
    {
        var now = DateTimeOffset.UtcNow;
        var flujo = new Flujo { Nombre = $"Flujo {Guid.NewGuid()}", CreatedAt = now, UpdatedAt = now };
        var version = new FlujoVersion { Flujo = flujo, NumeroVersion = 1, Estado = FlujoVersionEstado.Publicada, CreatedAt = now, PublishedAt = now };

        foreach (var (orden, nombre, tipo, configuracion) in pasos)
        {
            version.Pasos.Add(new FlujoPasoDef
            {
                FlujoVersion = version,
                Orden = orden,
                Nombre = nombre,
                TipoPaso = tipo,
                ConfiguracionJson = configuracion,
                CreatedAt = now,
            });
        }

        flujo.Versiones.Add(version);
        db.Add(flujo);
        await db.SaveChangesAsync();

        flujo.VersionActivaId = version.Id;

        var caso = new Caso
        {
            FlujoId = flujo.Id,
            FlujoVersionId = version.Id,
            Titulo = "Caso de prueba",
            DatosJson = datosJson,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Add(caso);
        await db.SaveChangesAsync();

        return caso;
    }

    [Fact]
    public async Task IniciarCasoAsync_LinearFlow_RunsEveryStepAndCompletesTheCaso()
    {
        using var db = TestDbContextFactory.Create();
        var orchestrator = new EjecucionOrchestrator(db, BuildExecutorProvider());

        var caso = await CrearCasoAsync(db, null,
            (1, "Paso A", TipoPaso.Interno, null),
            (2, "Paso B", TipoPaso.Interno, null));

        await orchestrator.IniciarCasoAsync(caso.Id, null, default);

        var casoActualizado = await db.Set<Caso>().SingleAsync(c => c.Id == caso.Id);
        Assert.Equal(CasoEstado.Completado, casoActualizado.Estado);

        var pasos = await db.Set<EjecucionPaso>().Where(p => p.CasoId == caso.Id).OrderBy(p => p.CreatedAt).ToListAsync();
        Assert.Equal(2, pasos.Count);
        Assert.All(pasos, p => Assert.Equal(EjecucionPasoEstado.Completado, p.Estado));
    }

    [Fact]
    public async Task IniciarCasoAsync_ConPasoInicial_SaltaLosPasosAnterioresComoOmitidos()
    {
        using var db = TestDbContextFactory.Create();
        var orchestrator = new EjecucionOrchestrator(db, BuildExecutorProvider());

        var caso = await CrearCasoAsync(db, null,
            (1, "Descargar", TipoPaso.Interno, null),
            (2, "Validar", TipoPaso.Interno, null),
            (3, "Registrar", TipoPaso.Interno, null));

        var pasoDefs = await db.Set<FlujoPasoDef>()
            .Where(p => p.FlujoVersionId == caso.FlujoVersionId)
            .OrderBy(p => p.Orden)
            .ToListAsync();
        var pasoRegistrar = pasoDefs.Single(p => p.Orden == 3);

        await orchestrator.IniciarCasoAsync(caso.Id, pasoRegistrar.Id, default);

        var pasos = await db.Set<EjecucionPaso>()
            .Where(p => p.CasoId == caso.Id)
            .Join(db.Set<FlujoPasoDef>(), p => p.FlujoPasoDefId, d => d.Id, (p, d) => new { p.Estado, d.Orden })
            .ToListAsync();

        Assert.Equal(EjecucionPasoEstado.Omitido, pasos.Single(p => p.Orden == 1).Estado);
        Assert.Equal(EjecucionPasoEstado.Omitido, pasos.Single(p => p.Orden == 2).Estado);
        Assert.Equal(EjecucionPasoEstado.Completado, pasos.Single(p => p.Orden == 3).Estado);

        var casoActualizado = await db.Set<Caso>().SingleAsync(c => c.Id == caso.Id);
        Assert.Equal(CasoEstado.Completado, casoActualizado.Estado);
    }

    [Fact]
    public async Task IniciarCasoAsync_DecisionStep_RoutesToTrueBranchAndMarksFalseBranchOmitido()
    {
        using var db = TestDbContextFactory.Create();
        var orchestrator = new EjecucionOrchestrator(db, BuildExecutorProvider());

        var caso = await CrearCasoAsync(db, """{"aprobado":true}""",
            (1, "Decidir", TipoPaso.Decision, """{"condicion":"aprobado","ordenSiVerdadero":2,"ordenSiFalso":3}"""),
            (2, "Rama SI", TipoPaso.Interno, null),
            (3, "Rama NO", TipoPaso.Interno, null));

        await orchestrator.IniciarCasoAsync(caso.Id, null, default);

        var pasos = await db.Set<EjecucionPaso>()
            .Where(p => p.CasoId == caso.Id)
            .Join(db.Set<FlujoPasoDef>(), p => p.FlujoPasoDefId, d => d.Id, (p, d) => new { p.Estado, d.Orden })
            .ToListAsync();

        Assert.Equal(EjecucionPasoEstado.Completado, pasos.Single(p => p.Orden == 1).Estado);
        Assert.Equal(EjecucionPasoEstado.Completado, pasos.Single(p => p.Orden == 2).Estado);
        Assert.Equal(EjecucionPasoEstado.Omitido, pasos.Single(p => p.Orden == 3).Estado);

        var casoActualizado = await db.Set<Caso>().SingleAsync(c => c.Id == caso.Id);
        Assert.Equal(CasoEstado.Completado, casoActualizado.Estado);
    }

    [Fact]
    public async Task ReprocesarPasoAsync_OnAFailedStep_CreatesANewAttemptWithoutTouchingTheFailedOne()
    {
        using var db = TestDbContextFactory.Create();
        var orchestrator = new EjecucionOrchestrator(db, BuildExecutorProvider());

        var caso = await CrearCasoAsync(db, null, (1, "Revisión", TipoPaso.RevisionHumana, null));
        await orchestrator.IniciarCasoAsync(caso.Id, null, default);

        var pasoEnEspera = await db.Set<EjecucionPaso>().SingleAsync(p => p.CasoId == caso.Id);
        await orchestrator.ResolverRevisionAsync(pasoEnEspera.Id, RevisionDecision.Rechazada, Guid.NewGuid(), "no cumple", default);

        var pasoFallido = await db.Set<EjecucionPaso>().SingleAsync(p => p.Id == pasoEnEspera.Id);
        Assert.Equal(EjecucionPasoEstado.Fallido, pasoFallido.Estado);
        Assert.Equal(1, pasoFallido.NumeroIntento);

        await orchestrator.ReprocesarPasoAsync(pasoFallido.Id, default);

        var intentos = await db.Set<EjecucionPaso>()
            .Where(p => p.EjecucionId == pasoFallido.EjecucionId && p.FlujoPasoDefId == pasoFallido.FlujoPasoDefId)
            .OrderBy(p => p.NumeroIntento)
            .ToListAsync();

        Assert.Equal(2, intentos.Count);
        Assert.Equal(EjecucionPasoEstado.Fallido, intentos[0].Estado); // el fallo original nunca se muta
        Assert.Equal(2, intentos[1].NumeroIntento);
        Assert.Equal(EjecucionPasoEstado.EsperandoRevisionHumana, intentos[1].Estado);
    }

    [Fact]
    public async Task ReprocesarPasoAsync_OnACancelledStep_ReopensCasoAndCreatesNewAttempt()
    {
        using var db = TestDbContextFactory.Create();
        var orchestrator = new EjecucionOrchestrator(db, BuildExecutorProvider());

        var caso = await CrearCasoAsync(db, null, (1, "Revisión", TipoPaso.RevisionHumana, null));
        await orchestrator.IniciarCasoAsync(caso.Id, null, default);
        await orchestrator.CancelarAsync(caso.Id, default);

        var pasoCancelado = await db.Set<EjecucionPaso>().SingleAsync(p => p.CasoId == caso.Id);
        Assert.Equal(EjecucionPasoEstado.Cancelado, pasoCancelado.Estado);

        await orchestrator.ReprocesarPasoAsync(pasoCancelado.Id, default);

        var casoReabierto = await db.Set<Caso>().SingleAsync(c => c.Id == caso.Id);
        Assert.Equal(CasoEstado.EsperandoRevisionHumana, casoReabierto.Estado);

        var intentos = await db.Set<EjecucionPaso>()
            .Where(p => p.EjecucionId == pasoCancelado.EjecucionId && p.FlujoPasoDefId == pasoCancelado.FlujoPasoDefId)
            .OrderBy(p => p.NumeroIntento)
            .ToListAsync();
        Assert.Equal(2, intentos.Count);
        Assert.Equal(EjecucionPasoEstado.Cancelado, intentos[0].Estado); // el intento cancelado nunca se muta
        Assert.Equal(EjecucionPasoEstado.EsperandoRevisionHumana, intentos[1].Estado);
    }

    [Fact]
    public async Task ReprocesarPasoAsync_OnAnOmitidoStep_Throws()
    {
        using var db = TestDbContextFactory.Create();
        var orchestrator = new EjecucionOrchestrator(db, BuildExecutorProvider());

        var caso = await CrearCasoAsync(db, """{"aprobado":true}""",
            (1, "Decidir", TipoPaso.Decision, """{"condicion":"aprobado","ordenSiVerdadero":2,"ordenSiFalso":3}"""),
            (2, "Rama SI", TipoPaso.Interno, null),
            (3, "Rama NO", TipoPaso.Interno, null));
        await orchestrator.IniciarCasoAsync(caso.Id, null, default);

        var pasoOmitido = await db.Set<EjecucionPaso>()
            .Join(db.Set<FlujoPasoDef>(), p => p.FlujoPasoDefId, d => d.Id, (p, d) => new { Paso = p, d.Orden })
            .Where(x => x.Paso.CasoId == caso.Id && x.Orden == 3)
            .Select(x => x.Paso)
            .SingleAsync();
        Assert.Equal(EjecucionPasoEstado.Omitido, pasoOmitido.Estado);

        await Assert.ThrowsAsync<InvalidOperationException>(() => orchestrator.ReprocesarPasoAsync(pasoOmitido.Id, default));
    }

    [Fact]
    public async Task CompletarCasoAsync_ClosesTheCasoEvenWithMoreStepsDefinedAfterwards()
    {
        using var db = TestDbContextFactory.Create();
        var orchestrator = new EjecucionOrchestrator(db, BuildExecutorProvider());

        var caso = await CrearCasoAsync(db, null,
            (1, "Revisión", TipoPaso.RevisionHumana, null),
            (2, "Nunca se alcanza", TipoPaso.Interno, null));
        await orchestrator.IniciarCasoAsync(caso.Id, null, default);

        var pasoEnEspera = await db.Set<EjecucionPaso>().SingleAsync(p => p.CasoId == caso.Id);

        // Simulate a worker marking its own step Completado directly (the same thing
        // RpaWorkerEndpoints.CompletarCasoAsync does before calling the orchestrator).
        pasoEnEspera.Estado = EjecucionPasoEstado.Completado;
        pasoEnEspera.FinishedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        await orchestrator.CompletarCasoAsync(pasoEnEspera.Id, default);

        var casoFinal = await db.Set<Caso>().SingleAsync(c => c.Id == caso.Id);
        Assert.Equal(CasoEstado.Completado, casoFinal.Estado);

        // Step 2 was never dispatched — CompletarCasoAsync skips the Orden-position walk entirely.
        var pasos = await db.Set<EjecucionPaso>().Where(p => p.CasoId == caso.Id).ToListAsync();
        Assert.Single(pasos);
    }

    [Fact]
    public async Task CancelarAsync_CreatesDescartadoEstadoAndReusesItOnLaterCancellations()
    {
        using var db = TestDbContextFactory.Create();
        var orchestrator = new EjecucionOrchestrator(db, BuildExecutorProvider());

        var caso1 = await CrearCasoAsync(db, null, (1, "Paso A", TipoPaso.RevisionHumana, null));
        await orchestrator.IniciarCasoAsync(caso1.Id, null, default);
        await orchestrator.CancelarAsync(caso1.Id, default);

        var estadosDescartado = await db.Set<FlujoEstadoDef>()
            .Where(e => e.FlujoId == caso1.FlujoId && e.Codigo == "DESCARTADO")
            .ToListAsync();
        Assert.Single(estadosDescartado);
        Assert.Equal("Descartado", estadosDescartado[0].Display);

        var caso1Actualizado = await db.Set<Caso>().SingleAsync(c => c.Id == caso1.Id);
        Assert.Equal(estadosDescartado[0].Id, caso1Actualizado.EstadoNegocioActualId);

        // Same Flujo, a second Caso cancelled later must reuse the same estado, not create a duplicate.
        var caso2 = new Caso
        {
            FlujoId = caso1.FlujoId,
            FlujoVersionId = caso1.FlujoVersionId,
            Titulo = "Segundo caso",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Add(caso2);
        await db.SaveChangesAsync();
        await orchestrator.IniciarCasoAsync(caso2.Id, null, default);
        await orchestrator.CancelarAsync(caso2.Id, default);

        var estadosDescartadoTrasSegundaCancelacion = await db.Set<FlujoEstadoDef>()
            .Where(e => e.FlujoId == caso1.FlujoId && e.Codigo == "DESCARTADO")
            .ToListAsync();
        Assert.Single(estadosDescartadoTrasSegundaCancelacion);

        var caso2Actualizado = await db.Set<Caso>().SingleAsync(c => c.Id == caso2.Id);
        Assert.Equal(estadosDescartado[0].Id, caso2Actualizado.EstadoNegocioActualId);
    }

    [Fact]
    public async Task CancelarAsync_CascadesToInFlightSteps()
    {
        using var db = TestDbContextFactory.Create();
        var orchestrator = new EjecucionOrchestrator(db, BuildExecutorProvider());

        var caso = await CrearCasoAsync(db, null, (1, "Revisión", TipoPaso.RevisionHumana, null));
        await orchestrator.IniciarCasoAsync(caso.Id, null, default);

        var pasoEnEspera = await db.Set<EjecucionPaso>().SingleAsync(p => p.CasoId == caso.Id);
        Assert.Equal(EjecucionPasoEstado.EsperandoRevisionHumana, pasoEnEspera.Estado);

        await orchestrator.CancelarAsync(caso.Id, default);

        var pasoTrasCancelar = await db.Set<EjecucionPaso>().SingleAsync(p => p.Id == pasoEnEspera.Id);
        Assert.Equal(EjecucionPasoEstado.Cancelado, pasoTrasCancelar.Estado);
        Assert.NotNull(pasoTrasCancelar.FinishedAt);
    }

    [Fact]
    public async Task PausarYReanudar_ReentraElPasoActualEnEsperaYLuegoResolverloCompletaElCaso()
    {
        using var db = TestDbContextFactory.Create();
        var orchestrator = new EjecucionOrchestrator(db, BuildExecutorProvider());

        var caso = await CrearCasoAsync(db, null,
            (1, "Revisión", TipoPaso.RevisionHumana, null),
            (2, "Final", TipoPaso.Interno, null));
        await orchestrator.IniciarCasoAsync(caso.Id, null, default);

        await orchestrator.PausarAsync(caso.Id, default);
        var casoPausado = await db.Set<Caso>().SingleAsync(c => c.Id == caso.Id);
        Assert.Equal(CasoEstado.Pausado, casoPausado.Estado);

        // Resuming re-enters the same in-flight attempt — the step is still waiting on a person, so
        // nothing advances yet, it just re-affirms EsperandoRevisionHumana.
        await orchestrator.ReanudarAsync(caso.Id, default);
        var casoTrasReanudar = await db.Set<Caso>().SingleAsync(c => c.Id == caso.Id);
        Assert.Equal(CasoEstado.EsperandoRevisionHumana, casoTrasReanudar.Estado);

        var pasoEnEspera = await db.Set<EjecucionPaso>().SingleAsync(p => p.CasoId == caso.Id);
        await orchestrator.ResolverRevisionAsync(pasoEnEspera.Id, RevisionDecision.Aprobada, Guid.NewGuid(), null, default);

        var casoFinal = await db.Set<Caso>().SingleAsync(c => c.Id == caso.Id);
        Assert.Equal(CasoEstado.Completado, casoFinal.Estado);
    }

    [Fact]
    public async Task IniciarCasoAsync_PasoConEstadoNegocioCodigo_ActualizaElCasoYRegistraElEvento()
    {
        using var db = TestDbContextFactory.Create();
        var orchestrator = new EjecucionOrchestrator(db, BuildExecutorProvider());

        var caso = await CrearCasoAsync(db, null,
            (1, "Paso A", TipoPaso.Interno, """{"estadoNegocioCodigo":"EN_BANCO"}"""),
            (2, "Paso B", TipoPaso.Interno, null));

        var now = DateTimeOffset.UtcNow;
        var estado = new FlujoEstadoDef { FlujoId = caso.FlujoId, Codigo = "EN_BANCO", Display = "En banco", Orden = 1, CreatedAt = now, UpdatedAt = now };
        db.Add(estado);
        await db.SaveChangesAsync();

        await orchestrator.IniciarCasoAsync(caso.Id, null, default);

        var casoActualizado = await db.Set<Caso>().SingleAsync(c => c.Id == caso.Id);
        Assert.Equal(estado.Id, casoActualizado.EstadoNegocioActualId);

        var evento = await db.Set<CasoEvento>().SingleAsync(e => e.CasoId == caso.Id && e.Accion == CasoEventoAccion.EstadoNegocioActualizado);
        Assert.Contains("EN_BANCO", evento.DetalleJson);
    }

    [Fact]
    public async Task IniciarCasoAsync_PasoSinEstadoNegocioCodigo_DejaElCampoSinTocar()
    {
        using var db = TestDbContextFactory.Create();
        var orchestrator = new EjecucionOrchestrator(db, BuildExecutorProvider());

        var caso = await CrearCasoAsync(db, null, (1, "Paso A", TipoPaso.Interno, null));

        await orchestrator.IniciarCasoAsync(caso.Id, null, default);

        var casoActualizado = await db.Set<Caso>().SingleAsync(c => c.Id == caso.Id);
        Assert.Null(casoActualizado.EstadoNegocioActualId);
    }

    [Fact]
    public async Task ResolverRevisionAsync_Aprobada_CompletesTheStepAndAdvances()
    {
        using var db = TestDbContextFactory.Create();
        var orchestrator = new EjecucionOrchestrator(db, BuildExecutorProvider());

        var caso = await CrearCasoAsync(db, null,
            (1, "Revisión", TipoPaso.RevisionHumana, null),
            (2, "Final", TipoPaso.Interno, null));
        await orchestrator.IniciarCasoAsync(caso.Id, null, default);

        var pasoEnEspera = await db.Set<EjecucionPaso>().SingleAsync(p => p.CasoId == caso.Id && p.NumeroIntento == 1);
        var revisorId = Guid.NewGuid();
        await orchestrator.ResolverRevisionAsync(pasoEnEspera.Id, RevisionDecision.Aprobada, revisorId, "todo bien", default);

        var revision = await db.Set<RevisionHumana>().SingleAsync(r => r.EjecucionPasoId == pasoEnEspera.Id);
        Assert.Equal(revisorId, revision.RevisorUserId);
        Assert.Equal(RevisionDecision.Aprobada, revision.Decision);

        var casoFinal = await db.Set<Caso>().SingleAsync(c => c.Id == caso.Id);
        Assert.Equal(CasoEstado.Completado, casoFinal.Estado);
    }
}
