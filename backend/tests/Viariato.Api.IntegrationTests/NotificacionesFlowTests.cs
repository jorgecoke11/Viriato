using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.ApiContracts;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Conexiones.Tipos;
using Viariato.Modules.Notificaciones.Domain;
using Viariato.Modules.Notificaciones.Envio;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Authorization;
using Viriato.Rpa.Client;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>
/// The pieces a price-watching robot needs from Viriato: memory between cases (the process store), its chart shown as an image,
/// accounts with the outside world (Conexiones), and notification rules that fire on what happens to a case and are sent by the
/// postman — through a fake kind of account here, which also proves the kinds are an extension point and not just "mail".
/// </summary>
public sealed class NotificacionesFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    /// <summary>A kind of account that "sends" by writing down, and can be told to fail.</summary>
    private sealed class CanalFalso : ITipoQueEnviaMensajes
    {
        public List<Mensaje> Enviados { get; } = [];
        public bool Fallar { get; set; }
        public string Codigo => "falso";
        public string Nombre => "Canal de pruebas";
        public string? EtiquetaSecreto => "Token";
        public IReadOnlyList<CampoDeConexion> Campos { get; } = [new("destino", "Destino", "texto", false)];
        public IReadOnlyList<string> Validar(JsonElement configuracion) => [];

        public Task EnviarAsync(JsonElement configuracion, string? secreto, Mensaje mensaje, CancellationToken ct)
        {
            if (Fallar) throw new ConexionException("El servidor de pruebas no contesta.");
            Assert.Equal("token-secreto", secreto);
            lock (Enviados) Enviados.Add(mensaje);
            return Task.CompletedTask;
        }
    }

    private readonly CanalFalso _canal = new();

    private WebApplicationFactory<Program> Host() =>
        factory.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddSingleton<ITipoDeConexion>(_canal)));

    private sealed record Entorno(
        WebApplicationFactory<Program> Host, HttpClient Admin, Guid FlujoId, Guid TipoId, Guid OtroTipoId, Guid PasoId, string Key, Guid ConexionId);

    private async Task<Entorno> CrearEntornoAsync(WebApplicationFactory<Program> host)
    {
        var admin = host.CreateClient();
        var email = $"avisos-{Guid.NewGuid():N}@example.com";
        var register = await admin.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "SuperSecret123", displayName = "Admin" });
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("id").GetGuid();
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rol = await db.Set<Role>().SingleAsync(r => r.Name == SystemRoles.Admin);
            db.Add(new UserRole { UserId = userId, RoleId = rol.Id, GrantedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        var login = await admin.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "SuperSecret123" });
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());

        var equipoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/equipos", new { nombre = $"Equipo {Guid.NewGuid():N}", descripcion = (string?)null }));
        var servicioId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Precios {Guid.NewGuid():N}", descripcion = (string?)null }));
        var flujoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Vigilancia {Guid.NewGuid():N}", descripcion = (string?)null }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/asignaciones", new { userId })).StatusCode);
        var tipoId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/tipos-caso", new { nombre = "MediaMarkt", orden = 1 }));
        var otroTipoId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/tipos-caso", new { nombre = "Amazon", orden = 2 }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync(
            $"/api/v1/flujos/{flujoId}/estados", new { codigo = "PRECIO_BAJADO", display = "Precio bajado", orden = 1, esFinal = false })).StatusCode);

        var versionId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new object[]
            {
                new { orden = 1, nombre = "Comprobar precio", tipoPaso = "Rpa", agenteDefinicionId = (Guid?)null, servicioId = (Guid?)servicioId, configuracionJson = "{\"aplicacion\":\"Tienda\"}" },
            },
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null)).StatusCode);
        var version = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{flujoId}/versiones/{versionId}");
        var pasoId = version.GetProperty("pasos")[0].GetProperty("id").GetGuid();

        var despliegue = await admin.PostAsJsonAsync("/api/v1/despliegues", new { equipoId, servicioId, flujoId });
        var key = (await despliegue.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("apiKey").GetString()!;

        var conexion = await admin.PostAsJsonAsync("/api/v1/conexiones", new
        {
            nombre = $"Pruebas {Guid.NewGuid():N}", tipo = "falso", configuracion = new { destino = "x" }, secreto = "token-secreto", activa = true,
        });
        return new Entorno(host, admin, flujoId, tipoId, otroTipoId, pasoId, key, await IdAsync(conexion));
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static RpaClient Robot(Entorno e)
    {
        var http = e.Host.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", e.Key);
        return new RpaClient(http);
    }

    /// <summary>A case of the given type, already taken by the robot: returns the claimed step.</summary>
    private static async Task<(Guid CasoId, Guid PasoId)> CasoEnManosDelRobotAsync(Entorno e, Guid tipoId, object datos)
    {
        var caso = await e.Admin.PostAsJsonAsync("/api/v1/casos", new
        {
            flujoId = e.FlujoId, titulo = $"Caso {Guid.NewGuid():N}", pasoInicialId = e.PasoId, tipoCasoId = tipoId, datosJson = JsonSerializer.Serialize(datos),
        });
        var casoId = await IdAsync(caso);
        var robot = Robot(e);
        await robot.ObtenerEstadoAsync();
        var asignada = await robot.ObtenerSiguienteEjecucionAsync();
        Assert.Equal(casoId, asignada!.CasoId);
        return (casoId, asignada.EjecucionPasoId);
    }

    private static Task<HttpResponseMessage> CrearReglaAsync(Entorno e, object regla) => e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/notificaciones", regla);

    private object ReglaDePrecio(Entorno e, Guid? tipoId, string evento = "precio_bajado") => new
    {
        nombre = $"Precio bajado {evento}", activa = true, disparador = "EventoDelRobot", codigo = evento, tipoCasoId = tipoId, conexionId = e.ConexionId,
        destinatarios = new[] { "jorge@example.com" },
        asunto = "Ha bajado {datos.producto}: {evento.ahora} €",
        cuerpo = "Antes {evento.antes} €, ahora {evento.ahora} €.\nCaso {caso} ({tipo}) del {fecha}.\n{enlace}",
    };

    private static async Task<List<EnvioDeNotificacion>> EnviosAsync(Entorno e, Guid casoId)
    {
        using var scope = e.Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<EnvioDeNotificacion>().AsNoTracking()
            .Where(x => x.CasoId == casoId).OrderBy(x => x.CreatedAt).ToListAsync();
    }

    private static async Task<List<CasoEventoAccion>> HistoriaAsync(Entorno e, Guid casoId)
    {
        using var scope = e.Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<CasoEvento>().AsNoTracking()
            .Where(x => x.CasoId == casoId).OrderBy(x => x.OccurredAt).Select(x => x.Accion).ToListAsync();
    }

    private static async Task<int> VueltaDelCarteroAsync(Entorno e, DateTimeOffset ahora)
    {
        using var scope = e.Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<BandejaDeSalida>().UnaVueltaAsync(ahora, CancellationToken.None);
    }

    // ---------------------------------------------------------------- the process store

    [Fact]
    public async Task El_almacen_guarda_lee_y_borra_y_otro_proceso_no_lo_ve()
    {
        using var host = Host();
        var e = await CrearEntornoAsync(host);
        var otro = await CrearEntornoAsync(host);
        var robot = Robot(e);

        Assert.Null(await robot.ObtenerDatoAsync("precio:mm:abc"));
        await robot.GuardarDatoAsync("precio:mm:abc", new[] { new { fecha = "2026-10-21", precio = 199.99 } });
        var guardado = await robot.ObtenerDatoAsync("precio:mm:abc");
        Assert.Equal(199.99, guardado!.Value[0].GetProperty("precio").GetDouble());

        // The same key in another process is another value: it does not exist there.
        Assert.Null(await Robot(otro).ObtenerDatoAsync("precio:mm:abc"));

        await robot.GuardarDatoAsync("precio:mm:abc", new { precio = 150 });
        Assert.Equal(150, (await robot.ObtenerDatoAsync("precio:mm:abc"))!.Value.GetProperty("precio").GetInt32());

        var http = host.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", e.Key);
        Assert.Equal(HttpStatusCode.NoContent, (await http.DeleteAsync("/api/v1/rpa/almacen/precio:mm:abc")).StatusCode);
        Assert.Null(await robot.ObtenerDatoAsync("precio:mm:abc"));
    }

    [Fact]
    public async Task El_almacen_rechaza_lo_que_pasa_de_64KB_las_claves_raras_y_a_quien_no_es_robot()
    {
        using var host = Host();
        var e = await CrearEntornoAsync(host);
        var http = host.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", e.Key);

        var grande = new StringContent(JsonSerializer.Serialize(new string('x', 70 * 1024)), Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await http.PutAsync("/api/v1/rpa/almacen/grande", grande)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PutAsync("/api/v1/rpa/almacen/con%20espacio", new StringContent("1", Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PutAsync("/api/v1/rpa/almacen/noesjson", new StringContent("{malo", Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClient().GetAsync("/api/v1/rpa/almacen/algo")).StatusCode);
    }

    [Fact]
    public async Task Quien_lee_el_proceso_ve_su_almacen_pero_no_el_de_otro_proceso()
    {
        using var host = Host();
        var e = await CrearEntornoAsync(host);
        var otro = await CrearEntornoAsync(host);
        await Robot(e).GuardarDatoAsync("precio:mm:abc", new { puntos = new[] { new { fecha = "2026-10-21", precio = 199.99 } } });
        await Robot(otro).GuardarDatoAsync("solo:del:otro", new { x = 1 });

        var lista = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/almacen");
        var dato = Assert.Single(lista.EnumerateArray());
        Assert.Equal("precio:mm:abc", dato.GetProperty("clave").GetString());
        Assert.True(dato.GetProperty("bytes").GetInt32() > 0);

        var leido = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/almacen/dato?clave={Uri.EscapeDataString("precio:mm:abc")}");
        Assert.Equal(199.99, JsonDocument.Parse(leido.GetProperty("valorJson").GetString()!).RootElement.GetProperty("puntos")[0].GetProperty("precio").GetDouble());

        // Another process's key is not reachable through this process's route.
        Assert.Equal(HttpStatusCode.NotFound, (await e.Admin.GetAsync($"/api/v1/flujos/{e.FlujoId}/almacen/dato?clave=solo:del:otro")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClient().GetAsync($"/api/v1/flujos/{e.FlujoId}/almacen")).StatusCode);
    }

    [Fact]
    public async Task Una_imagen_que_el_robot_sube_sin_decir_su_tipo_se_guarda_como_imagen()
    {
        using var host = Host();
        var e = await CrearEntornoAsync(host);
        var (casoId, pasoId) = await CasoEnManosDelRobotAsync(e, e.TipoId, new { producto = "Tele", url = "https://tienda.example/tele" });

        await Robot(e).AgregarEvidenciaAsync(pasoId, Viariato.ApiContracts.EvidenciaTipo.ArchivoGenerado, "Evolución del precio", null, new MemoryStream([0x89, 0x50, 0x4E, 0x47]), "grafico-precio.png");

        using var scope = host.Services.CreateScope();
        var documento = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Documento>().AsNoTracking().SingleAsync(d => d.CasoId == casoId);
        Assert.Equal("image/png", documento.ContentType);
    }

    // ---------------------------------------------------------------- accounts

    [Fact]
    public async Task Una_conexion_guarda_su_contraseña_cifrada_y_nunca_la_devuelve()
    {
        using var host = Host();
        var e = await CrearEntornoAsync(host);

        var lista = await e.Admin.GetFromJsonAsync<JsonElement>("/api/v1/conexiones");
        var mia = lista.EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == e.ConexionId);
        Assert.True(mia.GetProperty("tieneSecreto").GetBoolean());
        Assert.DoesNotContain("token-secreto", mia.GetRawText());

        using var scope = host.Services.CreateScope();
        var guardada = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Viariato.Modules.Conexiones.Domain.Conexion>().AsNoTracking()
            .SingleAsync(c => c.Id == e.ConexionId);
        Assert.DoesNotContain("token-secreto", guardada.SecretoCifrado);

        // A mail account without its server or with a sender that is not an address is refused, field by field.
        var mala = await e.Admin.PostAsJsonAsync("/api/v1/conexiones", new
        {
            nombre = $"Mala {Guid.NewGuid():N}", tipo = "correo", configuracion = new { puerto = 587, usuario = "a@b.c", remitente = "no-es-correo" }, secreto = "x",
        });
        Assert.Equal(HttpStatusCode.BadRequest, mala.StatusCode);
        Assert.Contains("Servidor SMTP", await mala.Content.ReadAsStringAsync());
    }

    // ---------------------------------------------------------------- rules and the outbox

    [Fact]
    public async Task El_aviso_del_robot_encola_el_correo_de_su_regla_con_los_datos_del_caso_y_del_aviso()
    {
        using var host = Host();
        var e = await CrearEntornoAsync(host);
        Assert.Equal(HttpStatusCode.OK, (await CrearReglaAsync(e, ReglaDePrecio(e, e.TipoId))).StatusCode);
        // Rules that must not fire: another type, another event.
        Assert.Equal(HttpStatusCode.OK, (await CrearReglaAsync(e, ReglaDePrecio(e, e.OtroTipoId))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CrearReglaAsync(e, ReglaDePrecio(e, null, "precio_subido"))).StatusCode);

        var (casoId, pasoId) = await CasoEnManosDelRobotAsync(e, e.TipoId, new { producto = "Tele 55", url = "https://tienda.example/tele" });
        await Robot(e).NotificarAsync(pasoId, "precio_bajado", new { antes = 499, ahora = 449.9 });

        var envio = Assert.Single(await EnviosAsync(e, casoId));
        Assert.Equal(EstadoDeEnvio.Pendiente, envio.Estado);
        Assert.Equal("Ha bajado Tele 55: 449.9 €", envio.Asunto);
        Assert.Contains("Antes 499 €, ahora 449.9 €.\nCaso ", envio.Cuerpo);
        Assert.Contains("(MediaMarkt)", envio.Cuerpo);
        Assert.Contains($"https://viriato.example/casos/{casoId}", envio.Cuerpo);
        Assert.Equal(["jorge@example.com"], envio.Destinatarios);
        Assert.Contains(CasoEventoAccion.AvisoDelRobot, await HistoriaAsync(e, casoId));
    }

    [Fact]
    public async Task Sin_reglas_el_aviso_queda_en_la_historia_y_no_se_envia_nada()
    {
        using var host = Host();
        var e = await CrearEntornoAsync(host);
        var (casoId, pasoId) = await CasoEnManosDelRobotAsync(e, e.TipoId, new { producto = "Tele" });

        await Robot(e).NotificarAsync(pasoId, "precio_bajado");

        Assert.Empty(await EnviosAsync(e, casoId));
        Assert.Contains(CasoEventoAccion.AvisoDelRobot, await HistoriaAsync(e, casoId));
    }

    [Fact]
    public async Task Llegar_a_un_estado_de_negocio_y_completar_el_caso_disparan_sus_reglas()
    {
        using var host = Host();
        var e = await CrearEntornoAsync(host);
        object Regla(string nombre, string disparador, string? codigo) => new
        {
            nombre, activa = true, disparador, codigo, tipoCasoId = (Guid?)null, conexionId = e.ConexionId, destinatarios = new[] { "jorge@example.com" },
            asunto = $"{nombre}: {{caso}}", cuerpo = "Estado: {estado}",
        };
        Assert.Equal(HttpStatusCode.OK, (await CrearReglaAsync(e, Regla("Bajó", "EstadoNegocio", "PRECIO_BAJADO"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CrearReglaAsync(e, Regla("Terminado", "CasoCompletado", null))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await CrearReglaAsync(e, Regla("Falló", "CasoFallido", null))).StatusCode);
        // A state the process does not have is refused when saving.
        Assert.Equal(HttpStatusCode.BadRequest, (await CrearReglaAsync(e, Regla("No existe", "EstadoNegocio", "NO_EXISTE"))).StatusCode);

        var (casoId, pasoId) = await CasoEnManosDelRobotAsync(e, e.OtroTipoId, new { producto = "Portátil" });
        var robot = Robot(e);
        await robot.CambiarEstadoCasoAsync(pasoId, "PRECIO_BAJADO");
        await robot.CompletarCasoAsync(pasoId);

        var envios = await EnviosAsync(e, casoId);
        Assert.Equal(["Bajó", "Terminado"], envios.Select(x => x.ReglaNombre).ToArray());
        Assert.Equal("Estado: Precio bajado", envios[0].Cuerpo);
    }

    [Fact]
    public async Task El_cartero_envia_lo_pendiente_por_la_conexion_y_lo_apunta_en_el_caso()
    {
        using var host = Host();
        var e = await CrearEntornoAsync(host);
        await CrearReglaAsync(e, ReglaDePrecio(e, null));
        var (casoId, pasoId) = await CasoEnManosDelRobotAsync(e, e.TipoId, new { producto = "Tele" });
        await Robot(e).NotificarAsync(pasoId, "precio_bajado", new { antes = 10, ahora = 8 });

        await VueltaDelCarteroAsync(e, DateTimeOffset.UtcNow);

        var envio = Assert.Single(await EnviosAsync(e, casoId));
        Assert.Equal(EstadoDeEnvio.Enviado, envio.Estado);
        Assert.Contains(_canal.Enviados, m => m.Asunto == "Ha bajado Tele: 8 €");
        Assert.Contains(CasoEventoAccion.NotificacionEnviada, await HistoriaAsync(e, casoId));

        // Already sent: another turn sends nothing more.
        var antes = _canal.Enviados.Count;
        await VueltaDelCarteroAsync(e, DateTimeOffset.UtcNow.AddHours(1));
        Assert.Equal(antes, _canal.Enviados.Count);
    }

    [Fact]
    public async Task Si_no_se_puede_enviar_reintenta_y_tras_cinco_intentos_queda_fallido_sin_tocar_el_caso()
    {
        using var host = Host();
        var e = await CrearEntornoAsync(host);
        await CrearReglaAsync(e, ReglaDePrecio(e, null));
        var (casoId, pasoId) = await CasoEnManosDelRobotAsync(e, e.TipoId, new { producto = "Tele" });
        var robot = Robot(e);
        await robot.NotificarAsync(pasoId, "precio_bajado", new { antes = 10, ahora = 8 });
        _canal.Fallar = true;

        var ahora = DateTimeOffset.UtcNow;
        await VueltaDelCarteroAsync(e, ahora);
        var envio = Assert.Single(await EnviosAsync(e, casoId));
        Assert.Equal((EstadoDeEnvio.Pendiente, 1, "El servidor de pruebas no contesta."), (envio.Estado, envio.Intentos, envio.UltimoError));
        Assert.True(envio.ProximoIntentoAt > ahora);

        // Not before its time…
        await VueltaDelCarteroAsync(e, ahora.AddSeconds(30));
        Assert.Equal(1, Assert.Single(await EnviosAsync(e, casoId)).Intentos);

        // …and after the 5th attempt it gives up.
        for (var i = 0; i < 4; i++) await VueltaDelCarteroAsync(e, ahora.AddDays(i + 1));
        envio = Assert.Single(await EnviosAsync(e, casoId));
        Assert.Equal((EstadoDeEnvio.Fallido, 5), (envio.Estado, envio.Intentos));
        Assert.Contains(CasoEventoAccion.NotificacionFallida, await HistoriaAsync(e, casoId));

        // The case did not notice: the robot finishes it as usual.
        await robot.CompletarCasoAsync(pasoId);

        // From the screen it can be sent again from zero.
        _canal.Fallar = false;
        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.PostAsync($"/api/v1/flujos/{e.FlujoId}/notificaciones/envios/{envio.Id}/reintentar", null)).StatusCode);
        await VueltaDelCarteroAsync(e, DateTimeOffset.UtcNow);
        Assert.Equal(EstadoDeEnvio.Enviado, Assert.Single(await EnviosAsync(e, casoId)).Estado);
    }

    [Fact]
    public async Task Una_regla_con_una_llave_que_no_existe_se_rechaza_al_guardarla()
    {
        using var host = Host();
        var e = await CrearEntornoAsync(host);
        var mala = await CrearReglaAsync(e, new
        {
            nombre = "Errata", activa = true, disparador = "EventoDelRobot", codigo = "precio_bajado", tipoCasoId = (Guid?)null, conexionId = e.ConexionId,
            destinatarios = new[] { "jorge@example.com" }, asunto = "Ha bajado {prodcto}", cuerpo = "x",
        });
        Assert.Equal(HttpStatusCode.BadRequest, mala.StatusCode);
        Assert.Contains("{prodcto}", await mala.Content.ReadAsStringAsync());
    }
}
