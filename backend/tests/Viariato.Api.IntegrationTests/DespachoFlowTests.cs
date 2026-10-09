using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Despacho;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.RpaFleet.Domain;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Authorization;
using Viriato.Rpa.Client;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>
/// Which robot of a machine goes first. Real robots (the client library) against the real API: two services on one
/// machine, each with work waiting, and the machine's order, limit and policy deciding who is handed what.
/// </summary>
public sealed class DespachoFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private sealed class Entorno(
        HttpClient admin, Guid equipoId, Guid s1, Guid s2, Guid d1, Guid d2, RpaClient robot1, RpaClient robot2, Guid flujoId, Guid paso1, Guid paso2)
    {
        public HttpClient Admin { get; } = admin;
        public Guid EquipoId { get; } = equipoId;
        public Guid Servicio1 { get; } = s1;
        public Guid Servicio2 { get; } = s2;
        public Guid Despliegue1 { get; } = d1;
        public Guid Despliegue2 { get; } = d2;
        public RpaClient Robot1 { get; } = robot1;
        public RpaClient Robot2 { get; } = robot2;
        public Guid FlujoId { get; } = flujoId;
        public Guid Paso1 { get; } = paso1;
        public Guid Paso2 { get; } = paso2;
    }

    private async Task<Entorno> CrearEntornoAsync()
    {
        var admin = factory.CreateClient();
        var email = $"despacho-{Guid.NewGuid():N}@example.com";

        var register = await admin.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "SuperSecret123", displayName = "Admin" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("id").GetGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rol = await db.Set<Role>().SingleAsync(r => r.Name == SystemRoles.Admin);
            db.Add(new UserRole { UserId = userId, RoleId = rol.Id, GrantedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var login = await admin.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "SuperSecret123" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var equipoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/equipos", new { nombre = $"Equipo {Guid.NewGuid():N}", descripcion = (string?)null }));
        var s1 = await IdAsync(await admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Servicio uno {Guid.NewGuid():N}", descripcion = (string?)null }));
        var s2 = await IdAsync(await admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Servicio dos {Guid.NewGuid():N}", descripcion = (string?)null }));
        var flujoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Proceso {Guid.NewGuid():N}", descripcion = (string?)null }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/asignaciones", new { userId })).StatusCode);

        // Step 1 is for service one and step 2 for service two: starting a Caso at one or the other puts work in the
        // queue of that service only.
        var versionId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        var pasos = await admin.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new object[]
            {
                new { orden = 1, nombre = "Uno", tipoPaso = "Rpa", agenteDefinicionId = (Guid?)null, servicioId = (Guid?)s1, configuracionJson = "{\"aplicacion\":\"A\"}" },
                new { orden = 2, nombre = "Dos", tipoPaso = "Rpa", agenteDefinicionId = (Guid?)null, servicioId = (Guid?)s2, configuracionJson = "{\"aplicacion\":\"B\"}" },
            },
        });
        Assert.Equal(HttpStatusCode.OK, pasos.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null)).StatusCode);
        var version = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{flujoId}/versiones/{versionId}");
        var ids = version.GetProperty("pasos").EnumerateArray().OrderBy(p => p.GetProperty("orden").GetInt32()).Select(p => p.GetProperty("id").GetGuid()).ToArray();

        var (d1, key1) = await DesplegarAsync(admin, equipoId, s1, flujoId);
        var (d2, key2) = await DesplegarAsync(admin, equipoId, s2, flujoId);

        return new Entorno(admin, equipoId, s1, s2, d1, d2, Robot(key1), Robot(key2), flujoId, ids[0], ids[1]);
    }

    private static async Task<(Guid Id, string Key)> DesplegarAsync(HttpClient admin, Guid equipoId, Guid servicioId, Guid flujoId)
    {
        var respuesta = await admin.PostAsJsonAsync("/api/v1/despliegues", new { equipoId, servicioId, flujoId });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return (cuerpo.GetProperty("despliegue").GetProperty("id").GetGuid(), cuerpo.GetProperty("apiKey").GetString()!);
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private RpaClient Robot(string apiKey)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return new RpaClient(http);
    }

    /// <summary>A Caso whose first pending step is the one of the given service.</summary>
    private static async Task<Guid> EncolarAsync(Entorno e, bool paraServicio1, string titulo)
    {
        var respuesta = await e.Admin.PostAsJsonAsync("/api/v1/casos", new
        {
            flujoId = e.FlujoId,
            titulo,
            pasoInicialId = paraServicio1 ? e.Paso1 : e.Paso2,
        });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>Makes both robots "alive" for the dispatcher: it only lets robots it has heard from compete.</summary>
    private static async Task ConectarAsync(Entorno e)
    {
        await e.Robot1.ObtenerEstadoAsync();
        await e.Robot2.ObtenerEstadoAsync();
    }

    private async Task ConfigurarAsync(Entorno e, int max, string politica, params Guid[] orden)
    {
        var respuesta = await e.Admin.PutAsJsonAsync($"/api/v1/equipos/{e.EquipoId}/despacho", new { maxEjecucionesSimultaneas = max, politica, orden });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    private async Task ModificarDespliegueAsync(Guid despliegueId, Action<Despliegue> cambio)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var despliegue = await db.Set<Despliegue>().SingleAsync(d => d.Id == despliegueId);
        cambio(despliegue);
        await db.SaveChangesAsync();
    }

    private async Task PonerTiempoMaximoAsync(Entorno e, Guid servicioId, int? minutos)
    {
        var cuerpo = minutos is { } m ? (object)new { tiempoMaximoMinutos = m } : new { quitarTiempoMaximo = true };
        var respuesta = await e.Admin.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/servicios/{servicioId}") { Content = JsonContent.Create(cuerpo) });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    /// <summary>Makes the step a robot holds for a Caso look as if the robot had claimed it that many minutes ago.</summary>
    private async Task ReclamadoHaceAsync(Guid casoId, int minutos)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var detalles = await (
            from d in db.Set<RpaEjecucionDetalle>()
            join p in db.Set<EjecucionPaso>() on d.EjecucionPasoId equals p.Id
            where p.CasoId == casoId && d.DespliegueId != null
            select d).ToListAsync();
        Assert.NotEmpty(detalles);
        foreach (var d in detalles) d.ClaimedAt = DateTimeOffset.UtcNow.AddMinutes(-minutos);
        await db.SaveChangesAsync();
    }

    /// <summary>One pass of the sweep that cancels overdue steps (the background one is switched off in the tests).</summary>
    private async Task<int> BarrerAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ControlDePasos>().CancelarVencidosAsync(DateTimeOffset.UtcNow, CancellationToken.None);
    }

    private async Task<(CasoEstado Caso, EjecucionEstado? Ejecucion, EjecucionPasoEstado[] Pasos, string?[] Errores)> EstadoDelCasoAsync(Guid casoId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var caso = await db.Set<Caso>().AsNoTracking().SingleAsync(c => c.Id == casoId);
        var ejecucion = caso.EjecucionActualId is { } id ? (EjecucionEstado?)(await db.Set<Ejecucion>().AsNoTracking().SingleAsync(x => x.Id == id)).Estado : null;
        var pasos = await db.Set<EjecucionPaso>().AsNoTracking().Where(p => p.CasoId == casoId).OrderBy(p => p.CreatedAt).ToListAsync();
        return (caso.Estado, ejecucion, pasos.Select(p => p.Estado).ToArray(), pasos.Select(p => p.ErrorMensaje).ToArray());
    }

    private static async Task TerminarAsync(RpaClient robot, EjecucionAsignadaDtoLike asignada) => await robot.CompletarCasoAsync(asignada.PasoId);

    private sealed record EjecucionAsignadaDtoLike(Guid PasoId, Guid CasoId);

    private static async Task<EjecucionAsignadaDtoLike?> PedirAsync(RpaClient robot)
    {
        var asignada = await robot.ObtenerSiguienteEjecucionAsync();
        return asignada is null ? null : new EjecucionAsignadaDtoLike(asignada.EjecucionPasoId, asignada.CasoId);
    }

    // ---------------------------------------------------------------- one at a time, first come first served

    [Fact]
    public async Task ByDefault_AMachineRunsOneStepAtATime_AndTheOldestWaitingGoesFirst()
    {
        var e = await CrearEntornoAsync();
        var antiguo = await EncolarAsync(e, paraServicio1: false, "Antiguo (servicio dos)");
        var reciente = await EncolarAsync(e, paraServicio1: true, "Reciente (servicio uno)");
        await ConectarAsync(e);

        // Service one has work too, but the older step is service two's: it is service two's turn.
        Assert.Null(await PedirAsync(e.Robot1));

        var primero = await PedirAsync(e.Robot2);
        Assert.Equal(antiguo, primero!.CasoId);

        // The machine is full now: nothing for service one until that step is done.
        Assert.Null(await PedirAsync(e.Robot1));

        await TerminarAsync(e.Robot2, primero);
        var segundo = await PedirAsync(e.Robot1);
        Assert.Equal(reciente, segundo!.CasoId);
    }

    // ---------------------------------------------------------------- priority

    [Fact]
    public async Task Prioridad_TheServiceHigherInTheMachinesOrderGoesFirst_EvenIfItsWorkIsNewer()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, 1, "Prioridad", e.Servicio1, e.Servicio2);
        var delDos = await EncolarAsync(e, paraServicio1: false, "Servicio dos, antiguo");
        var delUno = await EncolarAsync(e, paraServicio1: true, "Servicio uno, reciente");
        await ConectarAsync(e);

        Assert.Null(await PedirAsync(e.Robot2));
        var primero = await PedirAsync(e.Robot1);
        Assert.Equal(delUno, primero!.CasoId);

        await TerminarAsync(e.Robot1, primero);
        var segundo = await PedirAsync(e.Robot2);
        Assert.Equal(delDos, segundo!.CasoId);
    }

    [Fact]
    public async Task Prioridad_TheOrderIsPerMachine_AnotherMachineWithTheOppositeOrderServesTheOtherFirst()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, 1, "Prioridad", e.Servicio2, e.Servicio1);
        await EncolarAsync(e, paraServicio1: true, "Uno");
        var delDos = await EncolarAsync(e, paraServicio1: false, "Dos");
        await ConectarAsync(e);

        Assert.Null(await PedirAsync(e.Robot1));
        Assert.Equal(delDos, (await PedirAsync(e.Robot2))!.CasoId);
    }

    [Fact]
    public async Task AServiceLeftOutOfTheOrder_GoesAfterTheListedOnes()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, 1, "Prioridad", e.Servicio2);
        await EncolarAsync(e, paraServicio1: true, "Uno, antiguo y sin lista");
        var delDos = await EncolarAsync(e, paraServicio1: false, "Dos, listado");
        await ConectarAsync(e);

        Assert.Null(await PedirAsync(e.Robot1));
        Assert.Equal(delDos, (await PedirAsync(e.Robot2))!.CasoId);
    }

    // ---------------------------------------------------------------- how many at once

    [Fact]
    public async Task AMachineThatAllowsTwo_RunsBothServicesAtTheSameTime()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, 2, "Prioridad");
        var delDos = await EncolarAsync(e, paraServicio1: false, "Dos");
        var delUno = await EncolarAsync(e, paraServicio1: true, "Uno");
        await ConectarAsync(e);

        Assert.Equal(delDos, (await PedirAsync(e.Robot2))!.CasoId);
        Assert.Equal(delUno, (await PedirAsync(e.Robot1))!.CasoId);
    }

    [Fact]
    public async Task TheLimitIsPerMachine_AnotherMachineIsNotHeldUpByIt()
    {
        var e = await CrearEntornoAsync();
        var otroEquipo = await IdAsync(await e.Admin.PostAsJsonAsync("/api/v1/equipos", new { nombre = $"Otro {Guid.NewGuid():N}", descripcion = (string?)null }));
        var (_, keyOtro) = await DesplegarAsync(e.Admin, otroEquipo, e.Servicio1, e.FlujoId);
        var robotOtro = Robot(keyOtro);

        await EncolarAsync(e, paraServicio1: true, "Uno a");
        await EncolarAsync(e, paraServicio1: true, "Uno b");
        await ConectarAsync(e);
        await robotOtro.ObtenerEstadoAsync();

        var enPrimera = await PedirAsync(e.Robot1);
        var enOtra = await PedirAsync(robotOtro);

        Assert.NotNull(enPrimera);
        Assert.NotNull(enOtra);
        Assert.NotEqual(enPrimera.CasoId, enOtra.CasoId);
    }

    [Fact]
    public async Task TwoRobotsAskingAtTheSameInstant_NeverRunMoreThanTheMachineAllows()
    {
        var e = await CrearEntornoAsync();
        for (var i = 0; i < 4; i++) await EncolarAsync(e, paraServicio1: true, $"Uno {i}");
        for (var i = 0; i < 4; i++) await EncolarAsync(e, paraServicio1: false, $"Dos {i}");
        await ConectarAsync(e);

        var enMarcha = 0;
        var maximo = 0;
        var terminados = 0;
        var cuenta = new object();

        async Task Trabajar(RpaClient robot)
        {
            var sinTrabajo = 0;
            while (sinTrabajo < 40)
            {
                var asignada = await PedirAsync(robot);
                if (asignada is null)
                {
                    sinTrabajo++;
                    await Task.Delay(15);
                    continue;
                }

                sinTrabajo = 0;
                lock (cuenta)
                {
                    enMarcha++;
                    maximo = Math.Max(maximo, enMarcha);
                }

                await Task.Delay(40);
                lock (cuenta) enMarcha--;
                await robot.CompletarCasoAsync(asignada.PasoId);
                lock (cuenta) terminados++;
            }
        }

        await Task.WhenAll(Trabajar(e.Robot1), Trabajar(e.Robot2));

        Assert.Equal(8, terminados);
        Assert.Equal(1, maximo);
    }

    [Fact]
    public async Task TwoRobotsAskingInTheSameInstant_NeverBothGetWork_WhenThereIsRoomForOne()
    {
        var e = await CrearEntornoAsync();
        const int rondas = 30;
        for (var i = 0; i < rondas; i++)
        {
            await EncolarAsync(e, paraServicio1: true, $"Uno {i}");
            await EncolarAsync(e, paraServicio1: false, $"Dos {i}");
        }

        await ConectarAsync(e);

        for (var ronda = 0; ronda < rondas; ronda++)
        {
            // Both ask at once, with nothing running: exactly one slot, so exactly one of them may be told to go.
            var (a, b) = (PedirAsync(e.Robot1), PedirAsync(e.Robot2));
            await Task.WhenAll(a, b);

            var servidos = new[] { (Robot: e.Robot1, Asignada: a.Result), (Robot: e.Robot2, Asignada: b.Result) }.Where(x => x.Asignada is not null).ToList();
            Assert.True(servidos.Count == 1, $"round {ronda}: {servidos.Count} robots were handed work, the machine has room for one");

            await servidos[0].Robot.CompletarCasoAsync(servidos[0].Asignada!.PasoId);
        }
    }

    // ---------------------------------------------------------------- rotation

    [Fact]
    public async Task Turnos_ServicesTakeTurns_SoALongQueueOfOneDoesNotStarveTheOther()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, 1, "Turnos", e.Servicio1, e.Servicio2);
        var uno1 = await EncolarAsync(e, paraServicio1: true, "Uno 1");
        var uno2 = await EncolarAsync(e, paraServicio1: true, "Uno 2");
        var dos1 = await EncolarAsync(e, paraServicio1: false, "Dos 1");
        await ConectarAsync(e);

        var a = await PedirAsync(e.Robot1);
        Assert.Equal(uno1, a!.CasoId);
        await TerminarAsync(e.Robot1, a);

        // Prioridad would give service one its second step now; with turns it is service two's.
        Assert.Null(await PedirAsync(e.Robot1));
        var b = await PedirAsync(e.Robot2);
        Assert.Equal(dos1, b!.CasoId);
        await TerminarAsync(e.Robot2, b);

        var c = await PedirAsync(e.Robot1);
        Assert.Equal(uno2, c!.CasoId);
    }

    // ---------------------------------------------------------------- robots that are gone

    [Fact]
    public async Task ARobotThatHasNeverBeenHeardFrom_DoesNotHoldUpTheOnesBelowIt()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, 1, "Prioridad", e.Servicio1, e.Servicio2);
        await EncolarAsync(e, paraServicio1: true, "Uno, sin robot");
        var delDos = await EncolarAsync(e, paraServicio1: false, "Dos");

        // Only service two's robot is running: waiting for the higher-priority one would wait forever.
        var asignada = await PedirAsync(e.Robot2);

        Assert.Equal(delDos, asignada!.CasoId);
    }

    [Fact]
    public async Task ARobotThatStoppedAnswering_NoLongerHoldsUpTheOnesBelowIt()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, 1, "Prioridad", e.Servicio1, e.Servicio2);
        await EncolarAsync(e, paraServicio1: true, "Uno");
        var delDos = await EncolarAsync(e, paraServicio1: false, "Dos");
        await ConectarAsync(e);
        Assert.Null(await PedirAsync(e.Robot2));

        await ModificarDespliegueAsync(e.Despliegue1, d => d.LastUsedAt = DateTimeOffset.UtcNow.AddMinutes(-10));

        Assert.Equal(delDos, (await PedirAsync(e.Robot2))!.CasoId);
    }

    // ---------------------------------------------------------------- a service's maximum time

    [Fact]
    public async Task AStepThatOverrunsItsServicesMaximumTime_HasItsCasoCancelled()
    {
        var e = await CrearEntornoAsync();
        await PonerTiempoMaximoAsync(e, e.Servicio1, 30);
        var caso = await EncolarAsync(e, paraServicio1: true, "Se cuelga");
        await ConectarAsync(e);
        Assert.NotNull(await PedirAsync(e.Robot1));
        await ReclamadoHaceAsync(caso, 31);

        Assert.Equal(1, await BarrerAsync());

        var (estadoCaso, ejecucion, pasos, errores) = await EstadoDelCasoAsync(caso);
        Assert.Equal(CasoEstado.Cancelado, estadoCaso);
        Assert.Equal(EjecucionEstado.Cancelada, ejecucion);
        Assert.Equal([EjecucionPasoEstado.Cancelado], pasos);
        Assert.Contains("30 min", errores[0] ?? string.Empty);
    }

    [Fact]
    public async Task AStepWithinItsServicesMaximumTime_IsLeftAlone()
    {
        var e = await CrearEntornoAsync();
        await PonerTiempoMaximoAsync(e, e.Servicio1, 30);
        var caso = await EncolarAsync(e, paraServicio1: true, "Va bien");
        await ConectarAsync(e);
        Assert.NotNull(await PedirAsync(e.Robot1));
        await ReclamadoHaceAsync(caso, 29);

        Assert.Equal(0, await BarrerAsync());

        var (estadoCaso, _, pasos, _) = await EstadoDelCasoAsync(caso);
        Assert.Equal(CasoEstado.EnProgreso, estadoCaso);
        Assert.Equal([EjecucionPasoEstado.EnProgreso], pasos);
    }

    [Fact]
    public async Task AServiceWithoutAMaximumTime_NeverHasItsCasosCancelledForTakingLong()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Tarda mucho");
        await ConectarAsync(e);
        Assert.NotNull(await PedirAsync(e.Robot1));
        await ReclamadoHaceAsync(caso, 60 * 24 * 30);

        Assert.Equal(0, await BarrerAsync());
        Assert.Equal(CasoEstado.EnProgreso, (await EstadoDelCasoAsync(caso)).Caso);
    }

    [Fact]
    public async Task TheMaximumTimeIsPerService_TheOtherServiceRunsAsLongAsItNeeds()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, 2, "Prioridad");
        await PonerTiempoMaximoAsync(e, e.Servicio1, 10);
        var delUno = await EncolarAsync(e, paraServicio1: true, "Uno, con límite");
        var delDos = await EncolarAsync(e, paraServicio1: false, "Dos, sin límite");
        await ConectarAsync(e);
        Assert.NotNull(await PedirAsync(e.Robot1));
        Assert.NotNull(await PedirAsync(e.Robot2));
        await ReclamadoHaceAsync(delUno, 120);
        await ReclamadoHaceAsync(delDos, 120);

        Assert.Equal(1, await BarrerAsync());

        Assert.Equal(CasoEstado.Cancelado, (await EstadoDelCasoAsync(delUno)).Caso);
        Assert.Equal(CasoEstado.EnProgreso, (await EstadoDelCasoAsync(delDos)).Caso);
    }

    [Fact]
    public async Task AStepPastItsTime_NoLongerHoldsTheMachinesSlot_EvenBeforeTheSweepGetsToIt()
    {
        var e = await CrearEntornoAsync();
        await PonerTiempoMaximoAsync(e, e.Servicio1, 30);
        var colgado = await EncolarAsync(e, paraServicio1: true, "Uno, colgado");
        var delDos = await EncolarAsync(e, paraServicio1: false, "Dos");
        await ConectarAsync(e);
        Assert.NotNull(await PedirAsync(e.Robot1));

        // Robot one is still inside its time: the machine is full, nothing for service two.
        Assert.Null(await PedirAsync(e.Robot2));

        // It hangs past its time. Nobody has swept yet, but the slot is already free.
        await ReclamadoHaceAsync(colgado, 31);

        Assert.Equal(delDos, (await PedirAsync(e.Robot2))!.CasoId);
    }

    [Fact]
    public async Task AStepOfARobotThatCrashed_HoldsItsSlotUntilTheTimeIsUp_ThenTheCasoIsCancelledAndTheMachineMovesOn()
    {
        var e = await CrearEntornoAsync();
        await PonerTiempoMaximoAsync(e, e.Servicio1, 30);
        var caido = await EncolarAsync(e, paraServicio1: true, "Uno, el robot se cae");
        var delDos = await EncolarAsync(e, paraServicio1: false, "Dos");
        await ConectarAsync(e);
        Assert.NotNull(await PedirAsync(e.Robot1));

        // The robot dies without a word. For the platform that is indistinguishable from a long step: it waits.
        await ModificarDespliegueAsync(e.Despliegue1, d => d.LastUsedAt = DateTimeOffset.UtcNow.AddMinutes(-10));
        Assert.Null(await PedirAsync(e.Robot2));
        Assert.Equal(0, await BarrerAsync());

        // Until the service's time is up.
        await ReclamadoHaceAsync(caido, 31);
        Assert.Equal(1, await BarrerAsync());
        Assert.Equal(CasoEstado.Cancelado, (await EstadoDelCasoAsync(caido)).Caso);
        Assert.Equal(delDos, (await PedirAsync(e.Robot2))!.CasoId);
    }

    [Fact]
    public async Task ARobotAskingForWorkWhileHoldingAStep_KeepsItsStep()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Uno");
        await EncolarAsync(e, paraServicio1: true, "Otro");
        await ConectarAsync(e);
        var asignada = await PedirAsync(e.Robot1);
        Assert.NotNull(asignada);

        // It asks again (nothing in the platform treats that as "I forgot it"): the step is still its own.
        Assert.Null(await PedirAsync(e.Robot1));

        await e.Robot1.CompletarCasoAsync(asignada.PasoId);
        Assert.Equal(CasoEstado.Completado, (await EstadoDelCasoAsync(caso)).Caso);
    }

    [Fact]
    public async Task ARobotThatReportsAfterItsCasoWasCancelled_IsTurnedDown_AndTheCasoStaysCancelled()
    {
        var e = await CrearEntornoAsync();
        await PonerTiempoMaximoAsync(e, e.Servicio1, 5);
        var caso = await EncolarAsync(e, paraServicio1: true, "Tarda demasiado");
        await ConectarAsync(e);
        var asignada = await PedirAsync(e.Robot1);
        await ReclamadoHaceAsync(caso, 6);
        await BarrerAsync();

        var completar = await Assert.ThrowsAsync<ViriatoApiException>(() => e.Robot1.CompletarPasoAsync(asignada!.PasoId));
        var fallar = await Assert.ThrowsAsync<ViriatoApiException>(() => e.Robot1.FallarPasoAsync(asignada!.PasoId, "tarde"));
        var cerrar = await Assert.ThrowsAsync<ViriatoApiException>(() => e.Robot1.CompletarCasoAsync(asignada!.PasoId));

        Assert.All([completar, fallar, cerrar], ex => Assert.True(ex.IsConflict));
        var (estadoCaso, _, pasos, _) = await EstadoDelCasoAsync(caso);
        Assert.Equal(CasoEstado.Cancelado, estadoCaso);
        Assert.Equal([EjecucionPasoEstado.Cancelado], pasos);
    }

    [Fact]
    public async Task ARobotFinishingAtTheSameInstantTheSweepCutsItsStep_EitherWinsButNeverBoth()
    {
        var e = await CrearEntornoAsync();
        await PonerTiempoMaximoAsync(e, e.Servicio1, 5);

        const int rondas = 12;
        var ganoElRobot = 0;
        var ganoElBarrido = 0;
        for (var ronda = 0; ronda < rondas; ronda++)
        {
            var caso = await EncolarAsync(e, paraServicio1: true, $"Carrera {ronda}");
            await ConectarAsync(e);
            var asignada = await PedirAsync(e.Robot1);
            Assert.NotNull(asignada);
            await ReclamadoHaceAsync(caso, 6);

            var barrido = BarrerAsync();
            var robot = Task.Run(async () =>
            {
                try
                {
                    await e.Robot1.CompletarCasoAsync(asignada.PasoId);
                    return true;
                }
                catch (ViriatoApiException ex) when (ex.IsConflict)
                {
                    return false;
                }
            });
            await Task.WhenAll(barrido, robot);

            var (estadoCaso, _, pasos, _) = await EstadoDelCasoAsync(caso);
            if (await robot)
            {
                // The robot got there first: nothing to cut, and the Caso is what the robot made it.
                Assert.Equal(0, await barrido);
                Assert.Equal(CasoEstado.Completado, estadoCaso);
                Assert.Equal([EjecucionPasoEstado.Completado], pasos);
                ganoElRobot++;
            }
            else
            {
                Assert.Equal(1, await barrido);
                Assert.Equal(CasoEstado.Cancelado, estadoCaso);
                Assert.Equal([EjecucionPasoEstado.Cancelado], pasos);
                ganoElBarrido++;
            }
        }

        Assert.Equal(rondas, ganoElRobot + ganoElBarrido);
    }

    [Fact]
    public async Task TheBackgroundSweep_CancelsAnOverdueCaso_WithoutAnyoneAskingForIt()
    {
        var e = await CrearEntornoAsync();
        await PonerTiempoMaximoAsync(e, e.Servicio1, 5);
        var caso = await EncolarAsync(e, paraServicio1: true, "Lo corta el barrido");
        await ConectarAsync(e);
        Assert.NotNull(await PedirAsync(e.Robot1));
        await ReclamadoHaceAsync(caso, 6);
        Assert.Equal(CasoEstado.EnProgreso, (await EstadoDelCasoAsync(caso)).Caso);

        // A second instance of the API on the same database, sweeping every second. Nobody calls it: starting it is all it takes.
        using var conBarrido = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Despacho:BarridoSegundos"] = "1" })));
        using var cliente = conBarrido.CreateClient();

        var limite = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < limite && (await EstadoDelCasoAsync(caso)).Caso != CasoEstado.Cancelado)
        {
            await Task.Delay(300);
        }

        Assert.Equal(CasoEstado.Cancelado, (await EstadoDelCasoAsync(caso)).Caso);
    }

    [Fact]
    public async Task ARepeatedSweep_DoesNotCancelTheSameCasoTwice()
    {
        var e = await CrearEntornoAsync();
        await PonerTiempoMaximoAsync(e, e.Servicio1, 5);
        var caso = await EncolarAsync(e, paraServicio1: true, "Una vez");
        await ConectarAsync(e);
        Assert.NotNull(await PedirAsync(e.Robot1));
        await ReclamadoHaceAsync(caso, 6);

        Assert.Equal(1, await BarrerAsync());
        Assert.Equal(0, await BarrerAsync());
    }

    [Fact]
    public async Task TheMaximumTimeCanBeSetChangedAndRemoved_AndMustBeSensible()
    {
        var e = await CrearEntornoAsync();

        async Task<HttpResponseMessage> Parchear(object cuerpo) =>
            await e.Admin.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/servicios/{e.Servicio1}") { Content = JsonContent.Create(cuerpo) });

        var inicial = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/servicios/{e.Servicio1}");
        Assert.Equal(JsonValueKind.Null, inicial.GetProperty("tiempoMaximoMinutos").ValueKind);

        var puesto = await (await Parchear(new { tiempoMaximoMinutos = 45 })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(45, puesto.GetProperty("tiempoMaximoMinutos").GetInt32());

        // Saving the service without touching the time leaves it as it was.
        var intacto = await (await Parchear(new { descripcion = "otra" })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(45, intacto.GetProperty("tiempoMaximoMinutos").GetInt32());

        var quitado = await (await Parchear(new { quitarTiempoMaximo = true })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, quitado.GetProperty("tiempoMaximoMinutos").ValueKind);

        Assert.Equal(HttpStatusCode.BadRequest, (await Parchear(new { tiempoMaximoMinutos = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Parchear(new { tiempoMaximoMinutos = 10_081 })).StatusCode);

        var creado = await e.Admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Con tiempo {Guid.NewGuid():N}", descripcion = (string?)null, tiempoMaximoMinutos = 20 });
        Assert.Equal(20, (await creado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tiempoMaximoMinutos").GetInt32());
    }

    // ---------------------------------------------------------------- a service's cap across machines

    [Fact]
    public async Task AServiceCappedAcrossMachines_NeverRunsMoreStepsThanItsCap_NoMatterTheMachine()
    {
        var e = await CrearEntornoAsync();
        var cap = await e.Admin.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/servicios/{e.Servicio1}") { Content = JsonContent.Create(new { maxEjecucionesGlobales = 1 }) });
        Assert.Equal(HttpStatusCode.OK, cap.StatusCode);
        var otroEquipo = await IdAsync(await e.Admin.PostAsJsonAsync("/api/v1/equipos", new { nombre = $"Otro {Guid.NewGuid():N}", descripcion = (string?)null }));
        var (_, keyOtro) = await DesplegarAsync(e.Admin, otroEquipo, e.Servicio1, e.FlujoId);
        var robotOtro = Robot(keyOtro);

        await EncolarAsync(e, paraServicio1: true, "Uno a");
        await EncolarAsync(e, paraServicio1: true, "Uno b");
        await ConectarAsync(e);
        await robotOtro.ObtenerEstadoAsync();

        var primero = await PedirAsync(e.Robot1);
        Assert.NotNull(primero);

        // The other machine is idle and has work waiting, but the service is at its cap.
        Assert.Null(await PedirAsync(robotOtro));

        await TerminarAsync(e.Robot1, primero);
        Assert.NotNull(await PedirAsync(robotOtro));
    }

    [Fact]
    public async Task ARobotThatIsSwitchedOff_DoesNotHoldUpTheOthers()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, 1, "Prioridad", e.Servicio1, e.Servicio2);
        await EncolarAsync(e, paraServicio1: true, "Uno");
        var delDos = await EncolarAsync(e, paraServicio1: false, "Dos");
        await ConectarAsync(e);
        await ModificarDespliegueAsync(e.Despliegue1, d => d.Encendido = false);

        Assert.Equal(delDos, (await PedirAsync(e.Robot2))!.CasoId);
    }

    // ---------------------------------------------------------------- what the screen shows

    [Fact]
    public async Task TheQueueView_ListsWhatIsRunningAndWhatWaits_InTheOrderTheMachineWillServeIt()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, 1, "Prioridad", e.Servicio1, e.Servicio2);
        await EncolarAsync(e, paraServicio1: false, "Dos a");
        await EncolarAsync(e, paraServicio1: true, "Uno a");
        await EncolarAsync(e, paraServicio1: false, "Dos b");
        await ConectarAsync(e);
        Assert.NotNull(await PedirAsync(e.Robot1));
        await EncolarAsync(e, paraServicio1: true, "Uno b");

        var cola = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/equipos/{e.EquipoId}/despacho/cola");

        Assert.Equal(1, cola.GetProperty("maxEjecucionesSimultaneas").GetInt32());
        Assert.Equal(1, cola.GetProperty("enUso").GetInt32());
        Assert.Equal("Uno a", cola.GetProperty("enEjecucion")[0].GetProperty("casoTitulo").GetString());
        Assert.Equal(
            ["Uno b", "Dos a", "Dos b"],
            cola.GetProperty("pendientes").EnumerateArray().Select(p => p.GetProperty("casoTitulo").GetString()).ToArray());
        Assert.Equal(
            [1, 2, 3],
            cola.GetProperty("pendientes").EnumerateArray().Select(p => p.GetProperty("posicion").GetInt32()).ToArray());
        Assert.All(cola.GetProperty("pendientes").EnumerateArray(), p => Assert.True(p.GetProperty("robotConectado").GetBoolean()));
    }

    [Fact]
    public async Task TheQueueView_SaysWhenTheRobotThatWouldServeAStepIsNotConnected()
    {
        var e = await CrearEntornoAsync();
        await EncolarAsync(e, paraServicio1: true, "Uno");

        var cola = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/equipos/{e.EquipoId}/despacho/cola");

        var pendiente = cola.GetProperty("pendientes")[0];
        Assert.False(pendiente.GetProperty("robotConectado").GetBoolean());
        Assert.True(pendiente.GetProperty("robotEncendido").GetBoolean());
    }

    // ---------------------------------------------------------------- configuration

    [Fact]
    public async Task AMachineStartsWithOneStepAtATimeAndNoOrder_ListingTheServicesItRuns()
    {
        var e = await CrearEntornoAsync();

        var despacho = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/equipos/{e.EquipoId}/despacho");

        Assert.Equal(1, despacho.GetProperty("maxEjecucionesSimultaneas").GetInt32());
        Assert.Equal("Prioridad", despacho.GetProperty("politica").GetString());
        Assert.Equal(0, despacho.GetProperty("orden").GetArrayLength());
        Assert.Equal(2, despacho.GetProperty("serviciosDelEquipo").GetArrayLength());
    }

    [Fact]
    public async Task TheOrderIsStoredAsGiven_AndReplacedWholeOnTheNextSave()
    {
        var e = await CrearEntornoAsync();

        await ConfigurarAsync(e, 3, "Turnos", e.Servicio2, e.Servicio1);
        var primero = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/equipos/{e.EquipoId}/despacho");
        Assert.Equal(3, primero.GetProperty("maxEjecucionesSimultaneas").GetInt32());
        Assert.Equal("Turnos", primero.GetProperty("politica").GetString());
        Assert.Equal([e.Servicio2, e.Servicio1], primero.GetProperty("orden").EnumerateArray().Select(o => o.GetProperty("servicioId").GetGuid()).ToArray());

        await ConfigurarAsync(e, 1, "Prioridad", e.Servicio1);
        var segundo = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/equipos/{e.EquipoId}/despacho");
        Assert.Equal([e.Servicio1], segundo.GetProperty("orden").EnumerateArray().Select(o => o.GetProperty("servicioId").GetGuid()).ToArray());
    }

    [Fact]
    public async Task InvalidConfigurations_AreRejectedWithTheReason()
    {
        var e = await CrearEntornoAsync();
        var url = $"/api/v1/equipos/{e.EquipoId}/despacho";

        var cero = await e.Admin.PutAsJsonAsync(url, new { maxEjecucionesSimultaneas = 0, politica = "Prioridad", orden = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.BadRequest, cero.StatusCode);

        var politica = await e.Admin.PutAsJsonAsync(url, new { maxEjecucionesSimultaneas = 1, politica = "Aleatoria", orden = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.BadRequest, politica.StatusCode);

        var repetido = await e.Admin.PutAsJsonAsync(url, new { maxEjecucionesSimultaneas = 1, politica = "Prioridad", orden = new[] { e.Servicio1, e.Servicio1 } });
        Assert.Equal(HttpStatusCode.BadRequest, repetido.StatusCode);

        var inexistente = await e.Admin.PutAsJsonAsync(url, new { maxEjecucionesSimultaneas = 1, politica = "Prioridad", orden = new[] { Guid.NewGuid() } });
        Assert.Equal(HttpStatusCode.Conflict, inexistente.StatusCode);

        var sinEquipo = await e.Admin.PutAsJsonAsync($"/api/v1/equipos/{Guid.NewGuid()}/despacho", new { maxEjecucionesSimultaneas = 1, politica = "Prioridad", orden = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.NotFound, sinEquipo.StatusCode);
    }

    [Fact]
    public async Task ATemplate_AppliedToAMachine_IsCopiedNotLinked()
    {
        var e = await CrearEntornoAsync();
        var plantilla = await e.Admin.PostAsJsonAsync("/api/v1/plantillas-despacho", new
        {
            nombre = $"Plantilla {Guid.NewGuid():N}", descripcion = "El dos primero", maxEjecucionesSimultaneas = 2, politica = "Turnos", orden = new[] { e.Servicio2, e.Servicio1 },
        });
        Assert.Equal(HttpStatusCode.OK, plantilla.StatusCode);
        var plantillaId = (await plantilla.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var aplicada = await e.Admin.PostAsync($"/api/v1/equipos/{e.EquipoId}/despacho/plantilla/{plantillaId}", null);
        Assert.Equal(HttpStatusCode.OK, aplicada.StatusCode);
        var despacho = await aplicada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, despacho.GetProperty("maxEjecucionesSimultaneas").GetInt32());
        Assert.Equal("Turnos", despacho.GetProperty("politica").GetString());
        Assert.Equal([e.Servicio2, e.Servicio1], despacho.GetProperty("orden").EnumerateArray().Select(o => o.GetProperty("servicioId").GetGuid()).ToArray());

        // The machine now has its own copy: changing it leaves the template as it was...
        await ConfigurarAsync(e, 1, "Prioridad", e.Servicio1);
        var lista = await e.Admin.GetFromJsonAsync<JsonElement>("/api/v1/plantillas-despacho");
        var intacta = lista.EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == plantillaId);
        Assert.Equal(2, intacta.GetProperty("maxEjecucionesSimultaneas").GetInt32());
        Assert.Equal(2, intacta.GetProperty("orden").GetArrayLength());

        // ...and deleting the template leaves the machine as it is.
        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.DeleteAsync($"/api/v1/plantillas-despacho/{plantillaId}")).StatusCode);
        var maquina = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/equipos/{e.EquipoId}/despacho");
        Assert.Equal([e.Servicio1], maquina.GetProperty("orden").EnumerateArray().Select(o => o.GetProperty("servicioId").GetGuid()).ToArray());
    }

    [Fact]
    public async Task ATemplateCanBeAppliedToSeveralMachines_AndEachKeepsItsOwnAfterwards()
    {
        var e = await CrearEntornoAsync();
        var otro = await IdAsync(await e.Admin.PostAsJsonAsync("/api/v1/equipos", new { nombre = $"Otro {Guid.NewGuid():N}", descripcion = (string?)null }));
        var plantilla = await IdAsync(await e.Admin.PostAsJsonAsync("/api/v1/plantillas-despacho", new
        {
            nombre = $"Común {Guid.NewGuid():N}", descripcion = (string?)null, maxEjecucionesSimultaneas = 1, politica = "Prioridad", orden = new[] { e.Servicio1, e.Servicio2 },
        }));

        await e.Admin.PostAsync($"/api/v1/equipos/{e.EquipoId}/despacho/plantilla/{plantilla}", null);
        await e.Admin.PostAsync($"/api/v1/equipos/{otro}/despacho/plantilla/{plantilla}", null);
        await ConfigurarAsync(e, 1, "Prioridad", e.Servicio2, e.Servicio1);

        var segunda = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/equipos/{otro}/despacho");
        Assert.Equal([e.Servicio1, e.Servicio2], segunda.GetProperty("orden").EnumerateArray().Select(o => o.GetProperty("servicioId").GetGuid()).ToArray());
    }

    [Fact]
    public async Task Templates_CanBeEditedAndRenamed_ButNotGivenANameThatIsTaken()
    {
        var e = await CrearEntornoAsync();
        var nombre = $"Plantilla {Guid.NewGuid():N}";
        var otroNombre = $"Otra {Guid.NewGuid():N}";
        var id = await IdAsync(await e.Admin.PostAsJsonAsync("/api/v1/plantillas-despacho", new { nombre, descripcion = (string?)null, maxEjecucionesSimultaneas = 1, politica = "Prioridad", orden = new[] { e.Servicio1 } }));
        await e.Admin.PostAsJsonAsync("/api/v1/plantillas-despacho", new { nombre = otroNombre, descripcion = (string?)null, maxEjecucionesSimultaneas = 1, politica = "Prioridad", orden = Array.Empty<Guid>() });

        var repetido = await e.Admin.PostAsJsonAsync("/api/v1/plantillas-despacho", new { nombre, descripcion = (string?)null, maxEjecucionesSimultaneas = 1, politica = "Prioridad", orden = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);

        var choca = await e.Admin.PutAsJsonAsync($"/api/v1/plantillas-despacho/{id}", new { nombre = otroNombre, descripcion = (string?)null, maxEjecucionesSimultaneas = 1, politica = "Prioridad", orden = Array.Empty<Guid>() });
        Assert.Equal(HttpStatusCode.Conflict, choca.StatusCode);

        var editada = await e.Admin.PutAsJsonAsync($"/api/v1/plantillas-despacho/{id}", new { nombre = nombre + " v2", descripcion = "nueva", maxEjecucionesSimultaneas = 4, politica = "Turnos", orden = new[] { e.Servicio2, e.Servicio1 } });
        Assert.Equal(HttpStatusCode.OK, editada.StatusCode);
        var cuerpo = await editada.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(nombre + " v2", cuerpo.GetProperty("nombre").GetString());
        Assert.Equal(4, cuerpo.GetProperty("maxEjecucionesSimultaneas").GetInt32());
        Assert.Equal([e.Servicio2, e.Servicio1], cuerpo.GetProperty("orden").EnumerateArray().Select(o => o.GetProperty("servicioId").GetGuid()).ToArray());

        Assert.Equal(HttpStatusCode.NotFound, (await e.Admin.PutAsJsonAsync($"/api/v1/plantillas-despacho/{Guid.NewGuid()}", new { nombre = "x", descripcion = (string?)null, maxEjecucionesSimultaneas = 1, politica = "Prioridad", orden = Array.Empty<Guid>() })).StatusCode);
    }

    [Fact]
    public async Task OnlyWhoManagesTheFleet_CanSeeOrChangeHowAMachineDispatches()
    {
        var e = await CrearEntornoAsync();
        var otro = factory.CreateClient();
        var correo = $"sin-permiso-{Guid.NewGuid():N}@example.com";
        await otro.PostAsJsonAsync("/api/v1/auth/register", new { email = correo, password = "SuperSecret123", displayName = "Otro" });
        var login = await otro.PostAsJsonAsync("/api/v1/auth/login", new { email = correo, password = "SuperSecret123" });
        otro.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());

        Assert.Equal(HttpStatusCode.Forbidden, (await otro.GetAsync($"/api/v1/equipos/{e.EquipoId}/despacho")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otro.GetAsync($"/api/v1/equipos/{e.EquipoId}/despacho/cola")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otro.GetAsync("/api/v1/plantillas-despacho")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otro.PutAsJsonAsync($"/api/v1/equipos/{e.EquipoId}/despacho", new { maxEjecucionesSimultaneas = 5, politica = "Prioridad", orden = Array.Empty<Guid>() })).StatusCode);
    }

    [Fact]
    public async Task RemovingAServicioOrAMachine_TakesItsOrderEntriesWithIt()
    {
        var e = await CrearEntornoAsync();
        var sobrante = await IdAsync(await e.Admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Sobrante {Guid.NewGuid():N}", descripcion = (string?)null }));
        await ConfigurarAsync(e, 1, "Prioridad", e.Servicio1, sobrante);

        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.DeleteAsync($"/api/v1/servicios/{sobrante}")).StatusCode);

        var despacho = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/equipos/{e.EquipoId}/despacho");
        Assert.Equal([e.Servicio1], despacho.GetProperty("orden").EnumerateArray().Select(o => o.GetProperty("servicioId").GetGuid()).ToArray());
    }
}
