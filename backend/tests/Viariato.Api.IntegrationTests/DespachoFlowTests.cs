using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Viariato.Modules.Casos.AccionesMasivas;
using Microsoft.AspNetCore.Mvc.Testing;
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
        /// <summary>The API keys of the two Despliegues: every copy of a robot uses its Despliegue's.</summary>
        public string Key1 { get; init; } = string.Empty;
        public string Key2 { get; init; } = string.Empty;

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

    private async Task<Entorno> CrearEntornoAsync(WebApplicationFactory<Program>? anfitrion = null)
    {
        var host = anfitrion ?? factory;
        var admin = host.CreateClient();
        var email = $"despacho-{Guid.NewGuid():N}@example.com";

        var register = await admin.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "SuperSecret123", displayName = "Admin" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("id").GetGuid();

        using (var scope = host.Services.CreateScope())
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
        // A new machine has no ceiling (its robot copies set the capacity). Most of these tests are about a machine that
        // takes turns, so it starts with the ceiling of one; the ones about copies take it off.
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync(
            $"/api/v1/equipos/{equipoId}/despacho", new { maxEjecucionesSimultaneas = 1, politica = "Prioridad", orden = Array.Empty<Guid>() })).StatusCode);
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

        return new Entorno(admin, equipoId, s1, s2, d1, d2, Robot(key1), Robot(key2), flujoId, ids[0], ids[1]) { Key1 = key1, Key2 = key2 };
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

    private async Task ConfigurarAsync(Entorno e, int? max, string politica, params Guid[] orden)
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
    public async Task WithACeilingOfOne_AMachineRunsOneStepAtATime_AndTheOldestWaitingGoesFirst()
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

    // ---------------------------------------------------------------- following a run live

    private Task<HttpResponseMessage> EnVivoAsync(string apiKey, Guid pasoId, object cuerpo)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return http.PostAsJsonAsync($"/api/v1/rpa/pasos/{pasoId}/en-vivo", cuerpo);
    }

    [Fact]
    public async Task ARobotThatReportsItsProgress_ShowsUpOnTheCaso_AndTheList()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Caso en directo");
        await ConectarAsync(e);
        var asignada = await e.Robot1.ObtenerSiguienteEjecucionAsync();
        Assert.NotNull(asignada);

        var primero = await EnVivoAsync(e.Key1, asignada.EjecucionPasoId, new { vistaUrl = "http://localhost:6080/vnc.html", mensaje = "Entrando en el portal", porcentaje = 5 });
        var segundo = await EnVivoAsync(e.Key1, asignada.EjecucionPasoId, new { porcentaje = 40, mensaje = "Añadiendo productos a la cesta" });
        var tercero = await EnVivoAsync(e.Key1, asignada.EjecucionPasoId, new { porcentaje = 45 });
        Assert.Equal(HttpStatusCode.NoContent, primero.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, segundo.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, tercero.StatusCode);

        var ejecuciones = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{caso}/ejecuciones");
        var ejecucion = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{caso}/ejecuciones/{ejecuciones[0].GetProperty("id").GetGuid()}");
        var paso = ejecucion.GetProperty("pasos").EnumerateArray().Single(p => p.GetProperty("estado").GetString() == "EnProgreso");
        Assert.Equal(45, paso.GetProperty("progresoPorcentaje").GetInt32());
        Assert.Equal("Añadiendo productos a la cesta", paso.GetProperty("progresoMensaje").GetString());
        Assert.Equal("http://localhost:6080/vnc.html", paso.GetProperty("vistaEnDirectoUrl").GetString());

        var lista = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos?search={Uri.EscapeDataString("Caso en directo")}");
        var fila = lista.GetProperty("items").EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == caso);
        Assert.Equal(45, fila.GetProperty("progresoPorcentaje").GetInt32());
        Assert.True(fila.GetProperty("enVivo").GetBoolean());

        // How far a robot is lives on the step: the history of the Caso stays as it was.
        var timeline = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{caso}/timeline");
        Assert.DoesNotContain(timeline.EnumerateArray(), i => i.GetProperty("tipo").GetString() == "Hito");
    }

    [Fact]
    public async Task WhenTheStepEnds_TheLiveFiguresAndTheScreenLinkGoAway()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Caso que termina");
        await ConectarAsync(e);
        var asignada = (await PedirAsync(e.Robot1))!;
        await EnVivoAsync(e.Key1, asignada.PasoId, new { porcentaje = 90, vistaUrl = "https://vista.example.com/x" });

        await TerminarAsync(e.Robot1, asignada);

        // A late report is a conflict, like any other late report.
        Assert.Equal(HttpStatusCode.Conflict, (await EnVivoAsync(e.Key1, asignada.PasoId, new { porcentaje = 95 })).StatusCode);
        var lista = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos?search={Uri.EscapeDataString("Caso que termina")}");
        var fila = lista.GetProperty("items").EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == caso);
        Assert.False(fila.GetProperty("enVivo").GetBoolean());
        Assert.Equal(JsonValueKind.Null, fila.GetProperty("progresoPorcentaje").ValueKind);
    }

    [Fact]
    public async Task ABadLiveReport_IsRefused_AndAScreenLinkMustBeAWebAddress()
    {
        var e = await CrearEntornoAsync();
        await EncolarAsync(e, paraServicio1: true, "Caso con informe malo");
        await ConectarAsync(e);
        var asignada = (await PedirAsync(e.Robot1))!;

        Assert.Equal(HttpStatusCode.BadRequest, (await EnVivoAsync(e.Key1, asignada.PasoId, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await EnVivoAsync(e.Key1, asignada.PasoId, new { porcentaje = 101 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await EnVivoAsync(e.Key1, asignada.PasoId, new { porcentaje = -1 })).StatusCode);
        // It becomes a link in the interface: only http(s), never "javascript:".
        Assert.Equal(HttpStatusCode.BadRequest, (await EnVivoAsync(e.Key1, asignada.PasoId, new { vistaUrl = "javascript:alert(1)" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await EnVivoAsync(e.Key1, asignada.PasoId, new { vistaUrl = "/relativa" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await EnVivoAsync(e.Key1, asignada.PasoId, new { mensaje = new string('x', 201) })).StatusCode);
        // An empty text clears what was said.
        Assert.Equal(HttpStatusCode.NoContent, (await EnVivoAsync(e.Key1, asignada.PasoId, new { vistaUrl = "http://localhost:6080/", porcentaje = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await EnVivoAsync(e.Key1, asignada.PasoId, new { vistaUrl = "" })).StatusCode);
    }

    [Fact]
    public async Task ARobotCannotReportOnAStepOfAnotherRobot()
    {
        var e = await CrearEntornoAsync();
        await EncolarAsync(e, paraServicio1: true, "Caso ajeno");
        await ConectarAsync(e);
        var asignada = (await PedirAsync(e.Robot1))!;

        Assert.Equal(HttpStatusCode.NotFound, (await EnVivoAsync(e.Key2, asignada.PasoId, new { porcentaje = 10 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await EnVivoAsync(e.Key1, Guid.NewGuid(), new { porcentaje = 10 })).StatusCode);
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
    public async Task ACasoCutByTheMaximumTime_EndsOnItsOwnBusinessEstado_WhileOneCancelledByAPersonEndsOnDescartado()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, null, "Prioridad");
        await PonerTiempoMaximoAsync(e, e.Servicio1, 10);
        var porTiempo = await EncolarAsync(e, paraServicio1: true, "Se pasa de tiempo");
        var aMano = await EncolarAsync(e, paraServicio1: true, "Lo cancela una persona");
        var alOtroLado = await EncolarAsync(e, paraServicio1: true, "También se pasa");
        Assert.Equal(porTiempo, (await PedirAsync(Copia(e.Key1, "a")))!.CasoId);
        Assert.Equal(aMano, (await PedirAsync(Copia(e.Key1, "b")))!.CasoId);
        Assert.Equal(alOtroLado, (await PedirAsync(Copia(e.Key1, "c")))!.CasoId);

        await ReclamadoHaceAsync(porTiempo, 20);
        await ReclamadoHaceAsync(alOtroLado, 20);
        Assert.Equal(2, await BarrerAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.PostAsync($"/api/v1/casos/{aMano}/cancelar", null)).StatusCode);

        async Task<(string Codigo, string Display)> EstadoDeNegocioAsync(Guid casoId)
        {
            var caso = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}");
            var estado = caso.GetProperty("estadoNegocio");
            return (estado.GetProperty("codigo").GetString()!, estado.GetProperty("display").GetString()!);
        }

        // Technically all three are cancelled; for the process, the platform's cut says so, a person's does not. Two Casos of
        // the same process cut in the same sweep share the one estado (it is provisioned once).
        Assert.Equal(("CANCELADO_POR_TIEMPO", "Cancelado por exceso de tiempo de ejecución"), await EstadoDeNegocioAsync(porTiempo));
        Assert.Equal(("CANCELADO_POR_TIEMPO", "Cancelado por exceso de tiempo de ejecución"), await EstadoDeNegocioAsync(alOtroLado));
        Assert.Equal(("DESCARTADO", "Descartado"), await EstadoDeNegocioAsync(aMano));

        // And it is in the history of the Caso, final, like any other estado of the process.
        var historial = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{porTiempo}/timeline");
        Assert.Contains(historial.EnumerateArray(), i => i.GetProperty("tipo").GetString() == "EstadoCambiado"
            && i.GetProperty("titulo").GetString() == "Cancelado por exceso de tiempo de ejecución");
        var estados = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/estados");
        var delTiempo = estados.EnumerateArray().Single(s => s.GetProperty("codigo").GetString() == "CANCELADO_POR_TIEMPO");
        Assert.True(delTiempo.GetProperty("esFinal").GetBoolean());
    }

    [Fact]
    public async Task TheStepsOfARun_SayWhichServiceRunsThem_AndOnWhichMachineOnceARobotTookThem()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Con robot");
        await ConectarAsync(e);

        async Task<JsonElement> PasoAsync()
        {
            var ejecuciones = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{caso}/ejecuciones");
            var ejecucion = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{caso}/ejecuciones/{ejecuciones[0].GetProperty("id").GetGuid()}");
            return ejecucion.GetProperty("pasos").EnumerateArray().Single(p => p.GetProperty("tipoPaso").GetString() == "Rpa");
        }

        // Waiting: the service is known (the process says which one), the machine is not (nobody has taken it).
        var esperando = await PasoAsync();
        Assert.StartsWith("Servicio uno", esperando.GetProperty("servicioNombre").GetString());
        Assert.Equal(JsonValueKind.Null, esperando.GetProperty("equipoNombre").ValueKind);

        Assert.NotNull(await PedirAsync(e.Robot1));
        var enMarcha = await PasoAsync();
        Assert.StartsWith("Equipo ", enMarcha.GetProperty("equipoNombre").GetString());
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

    // ---------------------------------------------------------------- the priority of an execution

    /// <summary>The steps of the Caso's current run, as the API shows them (each RPA one carries its priority).</summary>
    private static async Task<List<JsonElement>> PasosDelCasoAsync(Entorno e, Guid casoId)
    {
        var caso = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}");
        return caso.GetProperty("ejecucionActual").GetProperty("pasos").EnumerateArray().ToList();
    }

    /// <summary>The execution of the Caso that is waiting for a robot right now.</summary>
    private static async Task<Guid> EjecucionEnColaAsync(Entorno e, Guid casoId) =>
        (await PasosDelCasoAsync(e, casoId)).Single(p => p.GetProperty("enCola").GetBoolean()).GetProperty("id").GetGuid();

    private static Task<HttpResponseMessage> CambiarPrioridadAsync(Entorno e, Guid casoId, Guid pasoId, int prioridad) =>
        e.Admin.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/casos/{casoId}/pasos/{pasoId}/prioridad")
        {
            Content = JsonContent.Create(new { prioridad }),
        });

    /// <summary>Gives the execution a Caso has waiting that priority.</summary>
    private static async Task PonerPrioridadAsync(Entorno e, Guid casoId, int prioridad)
    {
        var respuesta = await CambiarPrioridadAsync(e, casoId, await EjecucionEnColaAsync(e, casoId), prioridad);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(prioridad, (await respuesta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("prioridad").GetInt32());
    }

    /// <summary>The Casos service one's robot is handed one after the other, each closed before it asks again.</summary>
    private static async Task<Guid[]> ServidosPorElRobotUnoAsync(Entorno e, int cuantos)
    {
        var servidos = new List<Guid>();
        for (var i = 0; i < cuantos; i++)
        {
            var asignada = await PedirAsync(e.Robot1);
            Assert.NotNull(asignada);
            servidos.Add(asignada.CasoId);
            await TerminarAsync(e.Robot1, asignada);
        }

        return servidos.ToArray();
    }

    [Fact]
    public async Task AnExecutionIsCreatedWithPriorityZero_AndWaitingForARobot()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Normal");

        var paso = (await PasosDelCasoAsync(e, caso)).Single();
        Assert.Equal(0, paso.GetProperty("prioridad").GetInt32());
        Assert.True(paso.GetProperty("enCola").GetBoolean());
    }

    [Fact]
    public async Task WithinAService_TheExecutionWithTheHigherPriorityGoesFirst_EvenIfItArrivedLater()
    {
        var e = await CrearEntornoAsync();
        var primero = await EncolarAsync(e, paraServicio1: true, "Normal, el primero en llegar");
        var urgente = await EncolarAsync(e, paraServicio1: true, "Urgente, llega después");
        await PonerPrioridadAsync(e, urgente, 10);
        await ConectarAsync(e);

        Assert.Equal([urgente, primero], await ServidosPorElRobotUnoAsync(e, 2));
    }

    [Fact]
    public async Task WithinAService_ExecutionsOfTheSamePriority_KeepTheirArrivalOrder_AndANegativeOneWaitsForTheRest()
    {
        var e = await CrearEntornoAsync();
        var bajaPrimero = await EncolarAsync(e, paraServicio1: true, "Baja, llegó la primera");
        var normalA = await EncolarAsync(e, paraServicio1: true, "Normal A");
        var altaA = await EncolarAsync(e, paraServicio1: true, "Alta A");
        var normalB = await EncolarAsync(e, paraServicio1: true, "Normal B");
        var altaB = await EncolarAsync(e, paraServicio1: true, "Alta B");
        await PonerPrioridadAsync(e, bajaPrimero, -5);
        await PonerPrioridadAsync(e, altaA, 7);
        await PonerPrioridadAsync(e, altaB, 7);
        await ConectarAsync(e);

        Assert.Equal([altaA, altaB, normalA, normalB, bajaPrimero], await ServidosPorElRobotUnoAsync(e, 5));
    }

    [Fact]
    public async Task TheMachinesOrderOfServices_IsDecidedFirst_AnExecutionsPriorityOnlyOrdersItsOwnService()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, 1, "Prioridad", e.Servicio1, e.Servicio2);
        var delUno = await EncolarAsync(e, paraServicio1: true, "Servicio uno, prioridad normal");
        var delDosUrgente = await EncolarAsync(e, paraServicio1: false, "Servicio dos, muy urgente");
        await PonerPrioridadAsync(e, delDosUrgente, 1000);
        await ConectarAsync(e);

        // Service one is ranked first by the machine, whatever the priority of the execution waiting for service two.
        Assert.Null(await PedirAsync(e.Robot2));
        Assert.Equal(delUno, (await PedirAsync(e.Robot1))!.CasoId);
    }

    [Fact]
    public async Task ChangingThePriorityOfAWaitingExecution_ReordersTheQueueAtOnce()
    {
        var e = await CrearEntornoAsync();
        var a = await EncolarAsync(e, paraServicio1: true, "A");
        var b = await EncolarAsync(e, paraServicio1: true, "B");
        var c = await EncolarAsync(e, paraServicio1: true, "C");
        await ConectarAsync(e);

        await PonerPrioridadAsync(e, c, 3);
        var primero = await PedirAsync(e.Robot1);
        Assert.Equal(c, primero!.CasoId);
        await TerminarAsync(e.Robot1, primero);

        // B is promoted over A, which came first.
        await PonerPrioridadAsync(e, b, 1);
        Assert.Equal([b, a], await ServidosPorElRobotUnoAsync(e, 2));
    }

    [Fact]
    public async Task OnlyAnExecutionStillWaiting_CanChangeItsPriority_OneARobotHoldsIsRefused()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "En manos del robot");
        await ConectarAsync(e);
        var asignada = await PedirAsync(e.Robot1);
        Assert.NotNull(asignada);

        var enMarcha = await CambiarPrioridadAsync(e, caso, asignada.PasoId, 50);
        Assert.Equal(HttpStatusCode.Conflict, enMarcha.StatusCode);

        // The step the robot holds is untouched, and it can still finish it.
        await e.Robot1.CompletarCasoAsync(asignada.PasoId);
        Assert.Equal(CasoEstado.Completado, (await EstadoDelCasoAsync(caso)).Caso);

        var terminada = await CambiarPrioridadAsync(e, caso, asignada.PasoId, 50);
        Assert.Equal(HttpStatusCode.Conflict, terminada.StatusCode);
    }

    [Fact]
    public async Task TheNextStepsOfACaso_AreNewExecutions_AndStartAtZeroWhateverTheFirstOneHad()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Dos pasos");
        await PonerPrioridadAsync(e, caso, 9);
        await ConectarAsync(e);

        var primero = await PedirAsync(e.Robot1);
        Assert.NotNull(primero);
        await e.Robot1.CompletarPasoAsync(primero.PasoId);

        var pasos = await PasosDelCasoAsync(e, caso);
        var siguiente = pasos.Single(p => p.GetProperty("enCola").GetBoolean());
        Assert.Equal(0, siguiente.GetProperty("prioridad").GetInt32());
        Assert.Equal(9, pasos.Single(p => p.GetProperty("id").GetGuid() == primero.PasoId).GetProperty("prioridad").GetInt32());
    }

    [Fact]
    public async Task ReprocessingAnExecution_PutsItBackInTheQueueWithThePriorityItHad()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Se reprocesa");
        await PonerPrioridadAsync(e, caso, 6);
        await ConectarAsync(e);
        var asignada = await PedirAsync(e.Robot1);
        Assert.NotNull(asignada);
        await e.Robot1.FallarPasoAsync(asignada.PasoId, "falló");

        var reproceso = await e.Admin.PostAsync($"/api/v1/casos/{caso}/pasos/{asignada.PasoId}/reprocesar", null);
        Assert.Equal(HttpStatusCode.NoContent, reproceso.StatusCode);

        var nuevo = (await PasosDelCasoAsync(e, caso)).Single(p => p.GetProperty("enCola").GetBoolean());
        Assert.Equal(6, nuevo.GetProperty("prioridad").GetInt32());
        Assert.NotEqual(asignada.PasoId, nuevo.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task APriorityChange_IsRecordedInTheCasosHistory_OnlyWhenItActuallyChanges()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Con historial");
        var paso = await EjecucionEnColaAsync(e, caso);

        await PonerPrioridadAsync(e, caso, 4);
        await PonerPrioridadAsync(e, caso, 4);
        await PonerPrioridadAsync(e, caso, 9);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cambios = await db.Set<CasoEvento>().AsNoTracking()
            .Where(x => x.CasoId == caso && x.Accion == CasoEventoAccion.PrioridadCambiada).OrderBy(x => x.OccurredAt).ToListAsync();
        Assert.Equal(2, cambios.Count);
        var ultimo = JsonDocument.Parse(cambios[1].DetalleJson!).RootElement;
        Assert.Equal(9, ultimo.GetProperty("nueva").GetInt32());
        Assert.Equal(4, ultimo.GetProperty("anterior").GetInt32());
        Assert.Equal(paso, ultimo.GetProperty("ejecucionPasoId").GetGuid());
        Assert.NotNull(cambios[1].ActorUserId);
    }

    [Theory]
    [InlineData(1001)]
    [InlineData(-1001)]
    public async Task APriorityOutOfRange_IsRejected(int prioridad)
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Fuera de rango");

        var respuesta = await CambiarPrioridadAsync(e, caso, await EjecucionEnColaAsync(e, caso), prioridad);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task OnlyWhoCanSeeTheCaso_CanChangeAnExecutionsPriority_AndTheExecutionMustBelongToIt()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "De otro");
        var otroCaso = await EncolarAsync(e, paraServicio1: true, "Otro caso");
        var paso = await EjecucionEnColaAsync(e, caso);
        var ajeno = factory.CreateClient();
        var correo = $"ajeno-{Guid.NewGuid():N}@example.com";
        await ajeno.PostAsJsonAsync("/api/v1/auth/register", new { email = correo, password = "SuperSecret123", displayName = "Ajeno" });
        var login = await ajeno.PostAsJsonAsync("/api/v1/auth/login", new { email = correo, password = "SuperSecret123" });
        ajeno.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());

        var denegada = await ajeno.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/casos/{caso}/pasos/{paso}/prioridad") { Content = JsonContent.Create(new { prioridad = 5 }) });
        Assert.True(denegada.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);

        Assert.Equal(HttpStatusCode.NotFound, (await CambiarPrioridadAsync(e, Guid.NewGuid(), paso, 5)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CambiarPrioridadAsync(e, caso, Guid.NewGuid(), 5)).StatusCode);
        // An execution of another Caso is not reachable through this one.
        Assert.Equal(HttpStatusCode.NotFound, (await CambiarPrioridadAsync(e, otroCaso, paso, 5)).StatusCode);
        Assert.Equal(0, (await PasosDelCasoAsync(e, caso)).Single().GetProperty("prioridad").GetInt32());
    }

    [Fact]
    public async Task TheQueueView_ListsWhatWaitsByPriority_AndSaysWhichPriorityEachExecutionHas()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, 1, "Prioridad", e.Servicio1, e.Servicio2);
        var uno1 = await EncolarAsync(e, paraServicio1: true, "Uno normal");
        var uno2 = await EncolarAsync(e, paraServicio1: true, "Uno urgente");
        var dos1 = await EncolarAsync(e, paraServicio1: false, "Dos normal");
        var dos2 = await EncolarAsync(e, paraServicio1: false, "Dos urgente");
        await PonerPrioridadAsync(e, uno2, 5);
        await PonerPrioridadAsync(e, dos2, 50);
        await ConectarAsync(e);

        var cola = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/equipos/{e.EquipoId}/despacho/cola");
        var pendientes = cola.GetProperty("pendientes").EnumerateArray().ToList();

        // Service one is ranked first: its two executions by priority, then service two's two by priority.
        Assert.Equal(["Uno urgente", "Uno normal", "Dos urgente", "Dos normal"], pendientes.Select(p => p.GetProperty("casoTitulo").GetString()).ToArray());
        Assert.Equal([5, 0, 50, 0], pendientes.Select(p => p.GetProperty("prioridad").GetInt32()).ToArray());
        Assert.Equal([uno2, uno1, dos2, dos1], pendientes.Select(p => p.GetProperty("casoId").GetGuid()).ToArray());
        // Each one says which execution it is, so the screen can change its priority.
        Assert.Equal(await EjecucionEnColaAsync(e, uno2), pendientes[0].GetProperty("ejecucionPasoId").GetGuid());
    }

    // ---------------------------------------------------------------- pending vs. running

    [Fact]
    public async Task ACasoWaitingInTheQueue_IsPending_AndInProgressOnlyWhileARobotRunsIt()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Dos pasos");
        await ConectarAsync(e);

        // Created: its first step waits in the queue, nobody is running anything.
        Assert.Equal(CasoEstado.Pendiente, (await EstadoDelCasoAsync(caso)).Caso);

        // A robot takes it: now something is running.
        var primero = await PedirAsync(e.Robot1);
        Assert.NotNull(primero);
        Assert.Equal(CasoEstado.EnProgreso, (await EstadoDelCasoAsync(caso)).Caso);

        // It finishes the step; the Caso moves on to the next one, which another robot has to take: pending again.
        await e.Robot1.CompletarPasoAsync(primero.PasoId);
        Assert.Equal(CasoEstado.Pendiente, (await EstadoDelCasoAsync(caso)).Caso);

        var segundo = await PedirAsync(e.Robot2);
        Assert.NotNull(segundo);
        Assert.Equal(CasoEstado.EnProgreso, (await EstadoDelCasoAsync(caso)).Caso);

        await e.Robot2.CompletarCasoAsync(segundo.PasoId);
        Assert.Equal(CasoEstado.Completado, (await EstadoDelCasoAsync(caso)).Caso);
    }

    [Fact]
    public async Task TheSummary_CountsTheCasosARobotRunsApartFromThoseWaiting()
    {
        var e = await CrearEntornoAsync();
        await EncolarAsync(e, paraServicio1: true, "Uno");
        await EncolarAsync(e, paraServicio1: true, "Dos");
        await EncolarAsync(e, paraServicio1: true, "Tres");
        await ConectarAsync(e);

        async Task<(int Ejecutando, int Pendientes, int EnCurso)> ContarAsync()
        {
            var resumen = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/resumen?flujoId={e.FlujoId}");
            var tipo = resumen[0].GetProperty("porTipo")[0];
            return (tipo.GetProperty("enEjecucion").GetInt32(), tipo.GetProperty("pendientes").GetInt32(), tipo.GetProperty("enCurso").GetInt32());
        }

        Assert.Equal((0, 3, 3), await ContarAsync());

        var asignada = await PedirAsync(e.Robot1);
        Assert.NotNull(asignada);
        Assert.Equal((1, 2, 3), await ContarAsync());
    }

    [Fact]
    public async Task TheList_CanBeFilteredByPendingAndInProgress()
    {
        var e = await CrearEntornoAsync();
        var enMarcha = await EncolarAsync(e, paraServicio1: true, "En marcha");
        var enCola = await EncolarAsync(e, paraServicio1: true, "En cola");
        await ConectarAsync(e);
        var asignada = await PedirAsync(e.Robot1);
        Assert.Equal(enMarcha, asignada!.CasoId);

        async Task<Guid[]> ListarAsync(string estado)
        {
            var lista = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos?flujoId={e.FlujoId}&estado={estado}&pageSize=100");
            return lista.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray();
        }

        Assert.Equal([enMarcha], await ListarAsync("EnProgreso"));
        Assert.Equal([enCola], await ListarAsync("Pendiente"));
    }

    [Fact]
    public async Task TheSummaryGroups_AreExclusive_AndFollowWhatTheCasoIsDoing_NotItsBusinessEstado()
    {
        var e = await CrearEntornoAsync();
        var fallido = await EncolarAsync(e, paraServicio1: true, "Uno");
        var enMarcha = await EncolarAsync(e, paraServicio1: true, "Dos");
        var detenido = await EncolarAsync(e, paraServicio1: true, "Tres");
        var enCola = await EncolarAsync(e, paraServicio1: true, "Cuatro");
        await ConectarAsync(e);

        var primera = await PedirAsync(e.Robot1);
        Assert.Equal(fallido, primera!.CasoId);
        await e.Robot1.FallarPasoAsync(primera.PasoId, "falló");
        var segunda = await PedirAsync(e.Robot1);
        Assert.Equal(enMarcha, segunda!.CasoId);
        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.PostAsync($"/api/v1/casos/{detenido}/pausar", null)).StatusCode);

        var tipo = (await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/resumen?flujoId={e.FlujoId}"))[0].GetProperty("porTipo")[0];
        int Cuenta(string grupo) => tipo.GetProperty(grupo).GetInt32();

        // None of the Casos has a business estado: the groups come from what each one is doing.
        Assert.Equal((1, 1, 1, 1, 3), (Cuenta("enEjecucion"), Cuenta("pendientes"), Cuenta("detenidos"), Cuenta("finalizados"), Cuenta("enCurso")));

        async Task<Guid[]> ListarAsync(string consulta)
        {
            var lista = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos?flujoId={e.FlujoId}&pageSize=100&{consulta}");
            return lista.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray();
        }

        Assert.Equal([fallido], await ListarAsync("finalizado=true"));
        Assert.Equal(new HashSet<Guid> { enMarcha, detenido, enCola }, (await ListarAsync("finalizado=false")).ToHashSet());
        Assert.Equal([detenido], await ListarAsync("estado=Iniciado,Pausado,EsperandoRevisionHumana"));
    }

    [Fact]
    public async Task AReprocessedStep_GoesBackToTheQueue_AndTheCasoToPending()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Se reprocesa");
        await ConectarAsync(e);
        var asignada = await PedirAsync(e.Robot1);
        Assert.NotNull(asignada);
        await e.Robot1.FallarPasoAsync(asignada.PasoId, "falló");
        Assert.Equal(CasoEstado.Fallido, (await EstadoDelCasoAsync(caso)).Caso);

        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.PostAsync($"/api/v1/casos/{caso}/pasos/{asignada.PasoId}/reprocesar", null)).StatusCode);

        Assert.Equal(CasoEstado.Pendiente, (await EstadoDelCasoAsync(caso)).Caso);
    }

    [Fact]
    public async Task ACasoPausedWhileItWaited_IsPendingAgainWhenResumed_AndAClaimDoesNotUnpauseIt()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Se pausa en la cola");
        await ConectarAsync(e);

        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.PostAsync($"/api/v1/casos/{caso}/pausar", null)).StatusCode);
        Assert.Equal(CasoEstado.Pausado, (await EstadoDelCasoAsync(caso)).Caso);

        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.PostAsync($"/api/v1/casos/{caso}/reanudar", null)).StatusCode);
        Assert.Equal(CasoEstado.Pendiente, (await EstadoDelCasoAsync(caso)).Caso);

        // A robot taking a step of a Caso that is paused does not change what the Caso is.
        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.PostAsync($"/api/v1/casos/{caso}/pausar", null)).StatusCode);
        var asignada = await PedirAsync(e.Robot1);
        Assert.NotNull(asignada);
        Assert.Equal(CasoEstado.Pausado, (await EstadoDelCasoAsync(caso)).Caso);
    }

    // ---------------------------------------------------------------- cancelling an execution

    private static Task<HttpResponseMessage> CancelarEjecucionAsync(Entorno e, Guid casoId, Guid pasoId) =>
        e.Admin.PostAsync($"/api/v1/casos/{casoId}/pasos/{pasoId}/cancelar", null);

    [Fact]
    public async Task CancellingAnExecutionThatWaits_CancelsItAndItsCaso_AndNoRobotCanTakeItAnyMore()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Se cancela en la cola");
        var otro = await EncolarAsync(e, paraServicio1: true, "Sigue esperando");
        await ConectarAsync(e);
        var paso = await EjecucionEnColaAsync(e, caso);

        Assert.Equal(HttpStatusCode.NoContent, (await CancelarEjecucionAsync(e, caso, paso)).StatusCode);

        var (estadoCaso, ejecucion, pasos, errores) = await EstadoDelCasoAsync(caso);
        Assert.Equal(CasoEstado.Cancelado, estadoCaso);
        Assert.Equal(EjecucionEstado.Cancelada, ejecucion);
        Assert.Equal([EjecucionPasoEstado.Cancelado], pasos);
        Assert.Contains("manualmente", errores[0] ?? string.Empty);

        // The queue moves on: the cancelled one is gone from it, the other is next.
        Assert.Equal(otro, (await PedirAsync(e.Robot1))!.CasoId);
    }

    [Fact]
    public async Task CancellingAnExecutionARobotIsRunning_FreesTheMachine_AndWhatTheRobotReportsLaterIsTurnedDown()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Se cancela en marcha");
        var delDos = await EncolarAsync(e, paraServicio1: false, "Espera hueco");
        await ConectarAsync(e);
        var asignada = await PedirAsync(e.Robot1);
        Assert.NotNull(asignada);
        Assert.Null(await PedirAsync(e.Robot2));

        Assert.Equal(HttpStatusCode.NoContent, (await CancelarEjecucionAsync(e, caso, asignada.PasoId)).StatusCode);

        Assert.Equal(CasoEstado.Cancelado, (await EstadoDelCasoAsync(caso)).Caso);
        var tarde = await Assert.ThrowsAsync<ViriatoApiException>(() => e.Robot1.CompletarCasoAsync(asignada.PasoId));
        Assert.True(tarde.IsConflict);
        Assert.Equal(CasoEstado.Cancelado, (await EstadoDelCasoAsync(caso)).Caso);

        // The machine's slot is free again.
        Assert.Equal(delDos, (await PedirAsync(e.Robot2))!.CasoId);
    }

    [Fact]
    public async Task AnExecutionThatAlreadyFinished_CannotBeCancelled()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Ya terminó");
        await ConectarAsync(e);
        var asignada = await PedirAsync(e.Robot1);
        Assert.NotNull(asignada);
        await e.Robot1.CompletarCasoAsync(asignada.PasoId);

        Assert.Equal(HttpStatusCode.Conflict, (await CancelarEjecucionAsync(e, caso, asignada.PasoId)).StatusCode);
        Assert.Equal(CasoEstado.Completado, (await EstadoDelCasoAsync(caso)).Caso);
    }

    [Fact]
    public async Task CancellingAnExecutionTwice_TheSecondTimeIsAConflict()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Dos veces");
        var paso = await EjecucionEnColaAsync(e, caso);

        Assert.Equal(HttpStatusCode.NoContent, (await CancelarEjecucionAsync(e, caso, paso)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await CancelarEjecucionAsync(e, caso, paso)).StatusCode);
    }

    [Fact]
    public async Task OnlyWhoCanSeeTheCaso_CanCancelItsExecution_AndTheExecutionMustBelongToIt()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Ajena");
        var otroCaso = await EncolarAsync(e, paraServicio1: true, "Otro caso");
        var paso = await EjecucionEnColaAsync(e, caso);
        var ajeno = factory.CreateClient();
        var correo = $"ajeno-{Guid.NewGuid():N}@example.com";
        await ajeno.PostAsJsonAsync("/api/v1/auth/register", new { email = correo, password = "SuperSecret123", displayName = "Ajeno" });
        var login = await ajeno.PostAsJsonAsync("/api/v1/auth/login", new { email = correo, password = "SuperSecret123" });
        ajeno.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());

        var denegada = await ajeno.PostAsync($"/api/v1/casos/{caso}/pasos/{paso}/cancelar", null);
        Assert.True(denegada.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);

        Assert.Equal(HttpStatusCode.NotFound, (await CancelarEjecucionAsync(e, Guid.NewGuid(), paso)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CancelarEjecucionAsync(e, caso, Guid.NewGuid())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CancelarEjecucionAsync(e, otroCaso, paso)).StatusCode);
        Assert.Equal(CasoEstado.Pendiente, (await EstadoDelCasoAsync(caso)).Caso);
    }

    // ---------------------------------------------------------------- bulk actions on many Casos

    private static Task<HttpResponseMessage> EjecutarAccionAsync(HttpClient cliente, string accion, IEnumerable<Guid> ids, object? parametros = null) =>
        cliente.PostAsJsonAsync($"/api/v1/casos/acciones/{accion}", new { ids, parametros });

    private static Task<HttpResponseMessage> CancelarVariosAsync(HttpClient cliente, IEnumerable<Guid> ids) =>
        EjecutarAccionAsync(cliente, "cancelar", ids);

    [Fact]
    public async Task ABulkCancellation_CancelsTheActiveOnes_AndSaysWhyItLeftTheOthersAlone()
    {
        var e = await CrearEntornoAsync();

        // One Caso that is already finished…
        var terminado = await EncolarAsync(e, paraServicio1: false, "Terminado");
        await ConectarAsync(e);
        var delTerminado = await PedirAsync(e.Robot2);
        Assert.Equal(terminado, delTerminado!.CasoId);
        await e.Robot2.CompletarCasoAsync(delTerminado.PasoId);

        // …two waiting in the queue and one a robot is running.
        var uno = await EncolarAsync(e, paraServicio1: true, "En marcha");
        var dos = await EncolarAsync(e, paraServicio1: true, "Pendiente dos");
        var tres = await EncolarAsync(e, paraServicio1: true, "Pendiente tres");
        var asignada = await PedirAsync(e.Robot1);
        Assert.Equal(uno, asignada!.CasoId);
        var enMarcha = uno;
        var inexistente = Guid.NewGuid();

        var respuesta = await CancelarVariosAsync(e.Admin, [enMarcha, dos, tres, terminado, inexistente]);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, resultado.GetProperty("procesados").GetInt32());
        var omitidos = resultado.GetProperty("omitidos").EnumerateArray().ToDictionary(o => o.GetProperty("id").GetGuid(), o => o.GetProperty("motivo").GetString());
        Assert.Equal(2, omitidos.Count);
        Assert.Contains("finalizado", omitidos[terminado]);
        Assert.Contains("no encontrado", omitidos[inexistente]);

        foreach (var caso in new[] { enMarcha, dos, tres })
        {
            var (estado, ejecucion, pasos, _) = await EstadoDelCasoAsync(caso);
            Assert.Equal(CasoEstado.Cancelado, estado);
            Assert.Equal(EjecucionEstado.Cancelada, ejecucion);
            Assert.All(pasos, p => Assert.Equal(EjecucionPasoEstado.Cancelado, p));
        }

        Assert.Equal(CasoEstado.Completado, (await EstadoDelCasoAsync(terminado)).Caso);

        // Nothing is left in the queue for a robot to take, and what the robot that was running one reports is turned down.
        Assert.Null(await PedirAsync(e.Robot1));
        var tarde = await Assert.ThrowsAsync<ViriatoApiException>(() => e.Robot1.CompletarCasoAsync(asignada.PasoId));
        Assert.True(tarde.IsConflict);
    }

    [Fact]
    public async Task ABulkAction_ReportsAnAlreadyCancelledCasoAsLeftAlone_AndCountsEachOnceEvenIfRepeated()
    {
        var e = await CrearEntornoAsync();
        var caso = await EncolarAsync(e, paraServicio1: true, "Dos veces en la lista");
        var cancelado = await EncolarAsync(e, paraServicio1: true, "Ya cancelado");
        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.PostAsync($"/api/v1/casos/{cancelado}/cancelar", null)).StatusCode);

        var resultado = await (await CancelarVariosAsync(e.Admin, [caso, caso, cancelado])).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, resultado.GetProperty("procesados").GetInt32());
        var omitido = Assert.Single(resultado.GetProperty("omitidos").EnumerateArray());
        Assert.Equal(cancelado, omitido.GetProperty("id").GetGuid());
        Assert.Contains("cancelado", omitido.GetProperty("motivo").GetString());
    }

    [Fact]
    public async Task ABulkAction_CannotReachCasosOfAProcessTheCallerIsNotAssignedTo()
    {
        var e = await CrearEntornoAsync();
        var ajeno = await CrearEntornoAsync();
        var delAjeno = await EncolarAsync(ajeno, paraServicio1: true, "De otro proceso");

        var resultado = await (await CancelarVariosAsync(e.Admin, [delAjeno])).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, resultado.GetProperty("procesados").GetInt32());
        Assert.Contains("no encontrado", resultado.GetProperty("omitidos")[0].GetProperty("motivo").GetString());
        Assert.Equal(CasoEstado.Pendiente, (await EstadoDelCasoAsync(delAjeno)).Caso);
    }

    [Fact]
    public async Task ABulkAction_NeedsASelection_ThatIsNotAbsurdlyBig_AndThePermissionToUseTheBulkActions()
    {
        var e = await CrearEntornoAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await CancelarVariosAsync(e.Admin, [])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CancelarVariosAsync(e.Admin, Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()))).StatusCode);

        var sinPermiso = factory.CreateClient();
        var correo = $"sin-permiso-{Guid.NewGuid():N}@example.com";
        var registro = await sinPermiso.PostAsJsonAsync("/api/v1/auth/register", new { email = correo, password = "SuperSecret123", displayName = "Sin permiso" });
        var usuarioId = (await registro.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("id").GetGuid();
        // Registering gives the User role, which can already use the platform: "without permission" means without any role.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RemoveRange(await db.Set<Viariato.Modules.Users.Domain.UserRole>().Where(ur => ur.UserId == usuarioId).ToListAsync());
            await db.SaveChangesAsync();
        }
        var login = await sinPermiso.PostAsJsonAsync("/api/v1/auth/login", new { email = correo, password = "SuperSecret123" });
        sinPermiso.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await CancelarVariosAsync(sinPermiso, [Guid.NewGuid()])).StatusCode);
    }

    [Fact]
    public async Task AnActionThatDoesNotExist_IsNotFound()
    {
        var e = await CrearEntornoAsync();

        var respuesta = await EjecutarAccionAsync(e.Admin, "no-existe", [Guid.NewGuid()]);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    // The point of the design: a new action is one more class, and the endpoint, the selection checks and the report do not change.
    private sealed class AccionDePrueba : IAccionMasivaSobreCaso
    {
        public static readonly System.Collections.Concurrent.ConcurrentBag<(Guid Caso, int Valor)> Aplicadas = [];

        public string Id => "marcar-de-prueba";

        public string Permiso => Viariato.Shared.Authorization.Permissions.CasosManage;

        public string? ValidarParametros(JsonElement? parametros) =>
            parametros is { ValueKind: JsonValueKind.Object } p && p.TryGetProperty("valor", out var v) && v.TryGetInt32(out _) ? null : "Indica un valor entero.";

        public string? MotivoPorElQueNoAplica(CasoParaAccion caso) => caso.Estado == CasoEstado.Pausado ? null : "Solo se aplica a casos pausados.";

        public Task EjecutarAsync(CasoParaAccion caso, JsonElement? parametros, CancellationToken ct)
        {
            Aplicadas.Add((caso.Id, parametros!.Value.GetProperty("valor").GetInt32()));
            return Task.CompletedTask;
        }
    }

    private sealed class AccionQueNadiePuedeUsar : IAccionMasivaSobreCaso
    {
        public string Id => "reservada";

        public string Permiso => "permiso.que.nadie.tiene";

        public string? MotivoPorElQueNoAplica(CasoParaAccion caso) => null;

        public Task EjecutarAsync(CasoParaAccion caso, JsonElement? parametros, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task ANewAction_RegisteredInTheContainer_WorksThroughTheSameEndpoint_WithItsOwnRulesAndParameters()
    {
        using var conAcciones = factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.AddScoped<IAccionMasivaSobreCaso, AccionDePrueba>();
            s.AddScoped<IAccionMasivaSobreCaso, AccionQueNadiePuedeUsar>();
        }));
        var e = await CrearEntornoAsync(conAcciones);
        var pausado = await EncolarAsync(e, paraServicio1: true, "Pausado");
        var activo = await EncolarAsync(e, paraServicio1: true, "En cola");
        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.PostAsync($"/api/v1/casos/{pausado}/pausar", null)).StatusCode);

        // Its parameters are checked by the action itself.
        Assert.Equal(HttpStatusCode.BadRequest, (await EjecutarAccionAsync(e.Admin, "marcar-de-prueba", [pausado])).StatusCode);

        // Its applicability too: only the paused Caso receives it, the other is left alone with the action's own reason.
        var respuesta = await EjecutarAccionAsync(e.Admin, "marcar-de-prueba", [pausado, activo], new { valor = 7 });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, resultado.GetProperty("procesados").GetInt32());
        Assert.Equal("Solo se aplica a casos pausados.", resultado.GetProperty("omitidos")[0].GetProperty("motivo").GetString());
        Assert.Contains(AccionDePrueba.Aplicadas, a => a.Caso == pausado && a.Valor == 7);
        Assert.DoesNotContain(AccionDePrueba.Aplicadas, a => a.Caso == activo);

        // And each action brings its own permission: nobody has the one this asks for.
        Assert.Equal(HttpStatusCode.Forbidden, (await EjecutarAccionAsync(e.Admin, "reservada", [pausado])).StatusCode);

        // The cancel action keeps working next to them.
        Assert.Equal(1, (await (await CancelarVariosAsync(e.Admin, [activo])).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("procesados").GetInt32());
    }

    [Fact]
    public async Task TheListCanBeLimitedToTheCasosThatCanStillBeActedOn()
    {
        var e = await CrearEntornoAsync();
        var activo = await EncolarAsync(e, paraServicio1: true, "Activo");
        var cancelado = await EncolarAsync(e, paraServicio1: true, "Cancelado");
        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.PostAsync($"/api/v1/casos/{cancelado}/cancelar", null)).StatusCode);

        var todos = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos?flujoId={e.FlujoId}&pageSize=100");
        var soloActivos = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos?flujoId={e.FlujoId}&activos=true&pageSize=100");

        Assert.Equal(2, todos.GetProperty("total").GetInt32());
        Assert.Equal([activo], soloActivos.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray());
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

    // ---------------------------------------------------------------- copies of a robot (capacity comes from the stack)

    /// <summary>One running copy of a robot: the same API key as its Despliegue and an id of its own, like a replica of the
    /// container or one more process started on Windows.</summary>
    private RpaClient Copia(string apiKey, string instancia)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        http.DefaultRequestHeaders.Add("X-Viriato-Instancia", instancia);
        return new RpaClient(http);
    }

    [Fact]
    public async Task ANewMachine_HasNoCeiling_ItsCopiesSetTheCapacity()
    {
        var e = await CrearEntornoAsync();
        var nuevo = await IdAsync(await e.Admin.PostAsJsonAsync("/api/v1/equipos", new { nombre = $"Nuevo {Guid.NewGuid():N}", descripcion = (string?)null }));

        var despacho = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/equipos/{nuevo}/despacho");
        Assert.Equal(JsonValueKind.Null, despacho.GetProperty("maxEjecucionesSimultaneas").ValueKind);

        // Leaving it empty is a valid configuration; zero is not.
        Assert.Equal(HttpStatusCode.OK, (await e.Admin.PutAsJsonAsync($"/api/v1/equipos/{nuevo}/despacho", new { maxEjecucionesSimultaneas = (int?)null, politica = "Prioridad", orden = Array.Empty<Guid>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await e.Admin.PutAsJsonAsync($"/api/v1/equipos/{nuevo}/despacho", new { maxEjecucionesSimultaneas = 0, politica = "Prioridad", orden = Array.Empty<Guid>() })).StatusCode);
    }

    [Fact]
    public async Task ACopyStuckOnACase_DoesNotHoldUpTheOtherCopies()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, null, "Prioridad");
        var uno = await EncolarAsync(e, paraServicio1: true, "Uno, se atasca");
        var dos = await EncolarAsync(e, paraServicio1: true, "Dos");
        var tres = await EncolarAsync(e, paraServicio1: true, "Tres");
        var a = Copia(e.Key1, "copia-a");
        var b = Copia(e.Key1, "copia-b");
        var c = Copia(e.Key1, "copia-c");

        var deA = await PedirAsync(a);
        Assert.Equal(uno, deA!.CasoId);

        // A never finishes. B and C do not wait for it: they take the next ones at once.
        Assert.Equal(dos, (await PedirAsync(b))!.CasoId);
        Assert.Equal(tres, (await PedirAsync(c))!.CasoId);

        // A copy that is busy is not handed anything else, and there is nothing left anyway.
        Assert.Null(await PedirAsync(a));
        Assert.Null(await PedirAsync(b));
    }

    [Fact]
    public async Task ABusyCopyThatAsks_IsNotHandedASecondStep_EvenWhenAnotherCopyOfItsRobotIsFree()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, null, "Prioridad");
        await EncolarAsync(e, paraServicio1: true, "Uno");
        var a = Copia(e.Key1, "copia-a");
        var b = Copia(e.Key1, "copia-b");

        Assert.NotNull(await PedirAsync(a));
        Assert.Null(await PedirAsync(b));
        var dos = await EncolarAsync(e, paraServicio1: true, "Dos");

        // B is free, so the robot has work for a copy — but A, which holds a step, is not that copy.
        Assert.Null(await PedirAsync(a));
        Assert.Equal(dos, (await PedirAsync(b))!.CasoId);
    }

    [Fact]
    public async Task WithACeiling_TheCopiesAreHeldBackAsBefore()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, 1, "Prioridad");
        await EncolarAsync(e, paraServicio1: true, "Uno");
        await EncolarAsync(e, paraServicio1: true, "Dos");
        var a = Copia(e.Key1, "copia-a");
        var b = Copia(e.Key1, "copia-b");

        Assert.NotNull(await PedirAsync(a));
        Assert.Null(await PedirAsync(b));
    }

    [Fact]
    public async Task ARobotThatDoesNotSayWhichCopyItIs_IsOneCopy_AsItAlwaysWas()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, null, "Prioridad");
        await EncolarAsync(e, paraServicio1: true, "Uno");
        await EncolarAsync(e, paraServicio1: true, "Dos");

        Assert.NotNull(await PedirAsync(e.Robot1));
        // Same robot, no copy id: it is still the one that holds the first step, so it gets nothing more.
        Assert.Null(await PedirAsync(e.Robot1));
    }

    [Fact]
    public async Task ManyCopiesAskingAtOnce_ReceiveTheQueueInPriorityOrder()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, null, "Prioridad");
        var casos = new Dictionary<Guid, int>();
        foreach (var (titulo, prioridad) in new[] { ("a", 0), ("b", 5), ("c", -3), ("d", 9), ("e", 5), ("f", 1) })
        {
            var id = await EncolarAsync(e, paraServicio1: true, titulo);
            await PonerPrioridadAsync(e, id, prioridad);
            casos[id] = prioridad;
        }

        var copias = Enumerable.Range(0, 4).Select(i => Copia(e.Key1, $"copia-{i}")).ToList();
        var primeras = await Task.WhenAll(copias.Select(c => Task.Run(() => PedirAsync(c))));

        // Whoever asked first, the four that got something are the four with the highest priority.
        var recibidas = primeras.Select(p => p!.CasoId).ToList();
        Assert.Equal(4, recibidas.Distinct().Count());
        Assert.Equal(new[] { 9, 5, 5, 1 }, recibidas.Select(id => casos[id]).OrderByDescending(p => p));

        // And a copy that frees up next gets the best of what is left.
        await copias[0].CompletarCasoAsync(primeras[0]!.PasoId);
        var siguiente = await PedirAsync(copias[0]);
        Assert.Equal(0, casos[siguiente!.CasoId]);
    }

    [Fact]
    public async Task ACopyThatDies_OnlyLosesItsOwnStep_TheMaximumTimeCancelsIt_AndTheOthersKeepWorking()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, null, "Prioridad");
        await PonerTiempoMaximoAsync(e, e.Servicio1, 10);
        var muere = await EncolarAsync(e, paraServicio1: true, "Muere");
        var sigue = await EncolarAsync(e, paraServicio1: true, "Sigue");
        var despues = await EncolarAsync(e, paraServicio1: true, "Después");
        var a = Copia(e.Key1, "copia-a");
        var b = Copia(e.Key1, "copia-b");

        Assert.Equal(muere, (await PedirAsync(a))!.CasoId);
        Assert.Equal(sigue, (await PedirAsync(b))!.CasoId);

        // A is gone. Its step overruns: only its Caso is cancelled, B's is untouched.
        await ReclamadoHaceAsync(muere, 20);
        Assert.Equal(1, await BarrerAsync());
        Assert.Equal(CasoEstado.Cancelado, (await EstadoDelCasoAsync(muere)).Caso);
        Assert.Equal(CasoEstado.EnProgreso, (await EstadoDelCasoAsync(sigue)).Caso);

        // A replacement copy (new process, new id) takes the next case right away.
        Assert.Equal(despues, (await PedirAsync(Copia(e.Key1, "copia-nueva")))!.CasoId);
    }

    [Fact]
    public async Task TheMachinesQueue_SaysHowManyCopiesEachRobotHasAndHowManyAreFree()
    {
        var e = await CrearEntornoAsync();
        await ConfigurarAsync(e, null, "Prioridad");
        await EncolarAsync(e, paraServicio1: true, "Uno");
        var a = Copia(e.Key1, "copia-a");
        var b = Copia(e.Key1, "copia-b");

        Assert.NotNull(await PedirAsync(a));
        Assert.Null(await PedirAsync(b));

        var cola = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/equipos/{e.EquipoId}/despacho/cola");
        Assert.Equal(JsonValueKind.Null, cola.GetProperty("maxEjecucionesSimultaneas").ValueKind);
        var robot = cola.GetProperty("robots").EnumerateArray().Single(r => r.GetProperty("despliegueId").GetGuid() == e.Despliegue1);
        Assert.Equal(2, robot.GetProperty("instancias").GetInt32());
        Assert.Equal(1, robot.GetProperty("libres").GetInt32());
    }
}
