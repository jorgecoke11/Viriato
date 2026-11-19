using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.ApiContracts;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Familias;
using Viariato.Modules.Conexiones.Domain;
using Viariato.Modules.Notificaciones.Domain;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Authorization;
using Viriato.Rpa.Client;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>
/// Families: a group of cases that ends together. It is filled while open, closed by hand, by a robot or by itself on reaching its expected
/// members, and the family finisher ends it once every member has ended (and none is waiting for a reprocessing), opening its type's
/// closing case and firing the notices that listen for it.
/// </summary>
public sealed class FamiliasFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    /// <summary>A process with one robot step «Procesar» (worked by <see cref="Entorno.KeyTrabajador"/>), a launcher robot that may create
    /// cases in it (<see cref="Entorno.KeyLanzador"/>), a creator «Cierre» and a family type «Lote» that opens a case with it.</summary>
    private sealed record Entorno(HttpClient Admin, Guid FlujoId, Guid PasoId, string KeyTrabajador, string KeyLanzador, Guid TipoId, Guid CreadorId);

    private async Task<Entorno> CrearEntornoAsync(string condicion = "Siempre")
    {
        var admin = factory.CreateClient();
        var email = $"familias-{Guid.NewGuid():N}@example.com";
        var register = await admin.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "SuperSecret123", displayName = "Admin" });
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("id").GetGuid();
        using (var scope = factory.Services.CreateScope())
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
        var trabajador = await IdAsync(await admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Trabajador {Guid.NewGuid():N}", descripcion = (string?)null }));
        var lanzador = await IdAsync(await admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Lanzador {Guid.NewGuid():N}", descripcion = (string?)null }));
        var flujoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Extracción {Guid.NewGuid():N}", descripcion = (string?)null }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/asignaciones", new { userId })).StatusCode);

        var versionId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new object[]
            {
                new { orden = 1, nombre = "Procesar", tipoPaso = "Rpa", agenteDefinicionId = (Guid?)null, servicioId = (Guid?)trabajador, configuracionJson = "{\"aplicacion\":\"Portal\"}" },
            },
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null)).StatusCode);
        var version = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{flujoId}/versiones/{versionId}");
        var pasoId = version.GetProperty("pasos")[0].GetProperty("id").GetGuid();

        var keyTrabajador = (await (await admin.PostAsJsonAsync("/api/v1/despliegues", new { equipoId, servicioId = trabajador, flujoId }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("apiKey").GetString()!;
        var keyLanzador = (await (await admin.PostAsJsonAsync("/api/v1/despliegues", new { equipoId, servicioId = lanzador, flujoId, flujoDestinoId = flujoId }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("apiKey").GetString()!;

        var creadorId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/creadores", new
        {
            nombre = "Cierre", descripcion = (string?)null, tipoCasoId = (Guid?)null, formularioId = (Guid?)null, pasoInicialNombre = (string?)null,
            estadoNegocioInicialId = (Guid?)null, plantillaTitulo = "Cierre {datos.familia.nombre}", orden = 1,
        }));
        var tipoId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/tipos-de-familia", new
        {
            nombre = "Lote", descripcion = "Los casos de una extracción", activo = true, creadorAlTerminarId = creadorId, condicion,
        }));

        return new Entorno(admin, flujoId, pasoId, keyTrabajador, keyLanzador, tipoId, creadorId);
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private RpaClient Robot(string key)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", key);
        return new RpaClient(http);
    }

    private async Task<Guid> CrearCasoAsync(Entorno e) => await IdAsync(await e.Admin.PostAsJsonAsync("/api/v1/casos", new
    {
        flujoId = e.FlujoId, titulo = $"Caso {Guid.NewGuid():N}", pasoInicialId = e.PasoId,
    }));

    /// <summary>The worker robot takes the next step and ends it (completed or failed).</summary>
    private async Task<Guid> TrabajarAsync(Entorno e, bool bien = true)
    {
        var robot = Robot(e.KeyTrabajador);
        await robot.ObtenerEstadoAsync();
        var asignada = await robot.ObtenerSiguienteEjecucionAsync();
        Assert.NotNull(asignada);
        if (bien) await robot.CompletarPasoAsync(asignada.EjecucionPasoId);
        else await robot.FallarPasoAsync(asignada.EjecucionPasoId, "El portal no responde.");
        return asignada.CasoId;
    }

    private async Task<int> VueltaAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<TerminadorDeFamilias>().UnaVueltaAsync(DateTimeOffset.UtcNow, CancellationToken.None);
    }

    private async Task<Familia> FamiliaAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Familia>().AsNoTracking().FirstAsync(f => f.Id == id);
    }

    private async Task<List<FamiliaEventoAccion>> HistoriaAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<FamiliaEvento>().AsNoTracking()
            .Where(x => x.FamiliaId == id).OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).Select(x => x.Accion).ToListAsync();
    }

    private async Task<Caso> CasoAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Caso>().AsNoTracking().FirstAsync(c => c.Id == id);
    }

    [Fact]
    public async Task Un_robot_llena_y_cierra_una_familia_que_termina_cuando_acaban_sus_casos_y_abre_su_caso_de_cierre()
    {
        var e = await CrearEntornoAsync();
        var lanzador = Robot(e.KeyLanzador);

        var familia = await lanzador.CrearFamiliaAsync(new CrearFamiliaRobotRequest("lote", "Extracción 18/11"));
        Assert.Equal("Abierta", familia.Estado);
        Assert.Equal("Lote", familia.Tipo);
        var primero = await lanzador.CrearCasoAsync(new CrearCasoRobotRequest("Balay", """{"proveedor":"Balay"}""", Familias: [familia.Id]));
        var segundo = await lanzador.CrearCasoAsync(new CrearCasoRobotRequest("Bosch", """{"proveedor":"Bosch"}""", Familias: [familia.Id]));

        // Open: it does not end even if its cases are done, more could still join.
        Assert.Equal(0, await VueltaAsync());
        await lanzador.CerrarFamiliaAsync(familia.Id);
        await lanzador.CerrarFamiliaAsync(familia.Id); // twice is fine

        // Closed but its cases are still waiting for the robot.
        Assert.Equal(0, await VueltaAsync());
        await TrabajarAsync(e);
        Assert.Equal(0, await VueltaAsync());
        await TrabajarAsync(e);

        Assert.Equal(1, await VueltaAsync());
        Assert.Equal(0, await VueltaAsync()); // ended once

        var terminada = await FamiliaAsync(familia.Id);
        Assert.Equal(EstadoDeFamilia.Terminada, terminada.Estado);
        Assert.Equal((2, 0, 0), (terminada.Completados, terminada.Fallidos, terminada.Cancelados));
        Assert.Equal(
            [FamiliaEventoAccion.Creada, FamiliaEventoAccion.Cerrada, FamiliaEventoAccion.Terminada, FamiliaEventoAccion.CierreAbierto],
            await HistoriaAsync(familia.Id));

        // The closing case: born from the type's creator, with the family's summary as data.
        var cierre = await CasoAsync(terminada.CasoDeCierreId!.Value);
        Assert.Equal("Cierre Extracción 18/11", cierre.Titulo);
        var datos = JsonDocument.Parse(cierre.DatosJson!).RootElement.GetProperty("familia");
        Assert.Equal(familia.Id, datos.GetProperty("id").GetGuid());
        Assert.Equal(2, datos.GetProperty("total").GetInt32());
        Assert.Equal([primero.CasoId, segundo.CasoId], datos.GetProperty("casos").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()));

        // Its robot reads the members' own data.
        var leida = await Robot(e.KeyTrabajador).ObtenerFamiliaAsync(familia.Id);
        Assert.Equal("Terminada", leida.Estado);
        Assert.Equal(["Balay", "Bosch"],
            leida.Casos.Select(c => JsonDocument.Parse(c.DatosJson!).RootElement.GetProperty("proveedor").GetString()));

        // People see it with its progress, and the cases list filters by it.
        var vista = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/familias/{familia.Id}");
        Assert.Equal(2, vista.GetProperty("miembros").GetInt32());
        Assert.Equal(2, vista.GetProperty("terminados").GetInt32());
        Assert.Equal(cierre.Titulo, vista.GetProperty("casoDeCierreTitulo").GetString());
        var casos = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos?familiaId={familia.Id}");
        Assert.Equal(2, casos.GetProperty("total").GetInt32());
        var delCaso = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{primero.CasoId}/familias");
        Assert.Equal(familia.Id, Assert.Single(delCaso.EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Con_participantes_esperados_se_cierra_sola_y_no_admite_mas()
    {
        var e = await CrearEntornoAsync();
        var creada = await e.Admin.PostAsJsonAsync("/api/v1/familias", new
        {
            flujoId = e.FlujoId, tipoDeFamiliaId = e.TipoId, nombre = "Dos casos", participantesEsperados = 2, casoIds = new[] { await CrearCasoAsync(e) },
        });
        var familiaId = await IdAsync(creada);

        await VueltaAsync();
        Assert.Equal(EstadoDeFamilia.Abierta, (await FamiliaAsync(familiaId)).Estado);

        Assert.Equal(HttpStatusCode.OK, (await e.Admin.PostAsJsonAsync($"/api/v1/familias/{familiaId}/casos", new { casoIds = new[] { await CrearCasoAsync(e) } })).StatusCode);
        var tercero = await CrearCasoAsync(e);
        Assert.Equal(HttpStatusCode.Conflict, (await e.Admin.PostAsJsonAsync($"/api/v1/familias/{familiaId}/casos", new { casoIds = new[] { tercero } })).StatusCode);

        await VueltaAsync();
        var familia = await FamiliaAsync(familiaId);
        Assert.Equal(EstadoDeFamilia.Cerrada, familia.Estado);
        Assert.Equal(HttpStatusCode.Conflict, (await e.Admin.PostAsJsonAsync($"/api/v1/familias/{familiaId}/casos", new { casoIds = new[] { tercero } })).StatusCode);
    }

    [Fact]
    public async Task Un_caso_que_espera_reprocesamiento_no_ha_terminado()
    {
        var e = await CrearEntornoAsync();
        Assert.Equal(HttpStatusCode.OK, (await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/reprocesamientos", new
        {
            nombre = "Reintentar", activa = true, orden = 0, estadoNegocioId = (Guid?)null, estadoEjecucion = "Fallido", pasoNombre = (string?)null,
            esperaMinutos = 30, limite = (int?)null, estadoFinalId = (Guid?)null,
        })).StatusCode);

        var casoId = await CrearCasoAsync(e);
        var familiaId = await IdAsync(await e.Admin.PostAsJsonAsync("/api/v1/familias", new
        {
            flujoId = e.FlujoId, tipoDeFamiliaId = e.TipoId, nombre = "Con reintento", participantesEsperados = (int?)null, casoIds = new[] { casoId },
        }));
        Assert.Equal(HttpStatusCode.OK, (await e.Admin.PostAsync($"/api/v1/familias/{familiaId}/cerrar", null)).StatusCode);

        await TrabajarAsync(e, bien: false);
        Assert.Equal(CasoEstado.Fallido, (await CasoAsync(casoId)).Estado);

        await VueltaAsync();
        Assert.Equal(EstadoDeFamilia.Cerrada, (await FamiliaAsync(familiaId)).Estado);
    }

    [Fact]
    public async Task Solo_si_todos_bien_no_abre_el_caso_de_cierre_si_alguno_fallo_y_dice_por_que()
    {
        var e = await CrearEntornoAsync(condicion: "SoloSiTodosBien");
        var bien = await CrearCasoAsync(e);
        var cancelado = await CrearCasoAsync(e);
        var familiaId = await IdAsync(await e.Admin.PostAsJsonAsync("/api/v1/familias", new
        {
            flujoId = e.FlujoId, tipoDeFamiliaId = e.TipoId, nombre = "Exigente", participantesEsperados = (int?)null, casoIds = new[] { bien, cancelado },
        }));
        await e.Admin.PostAsync($"/api/v1/familias/{familiaId}/cerrar", null);

        Assert.True((await e.Admin.PostAsync($"/api/v1/casos/{cancelado}/cancelar", null)).IsSuccessStatusCode);
        await TrabajarAsync(e);

        Assert.Equal(1, await VueltaAsync());
        var familia = await FamiliaAsync(familiaId);
        Assert.Equal(EstadoDeFamilia.Terminada, familia.Estado);
        Assert.Equal((1, 0, 1), (familia.Completados, familia.Fallidos, familia.Cancelados));
        Assert.Null(familia.CasoDeCierreId);
        Assert.Contains("completados", familia.NotaDeCierre);
        Assert.Equal(FamiliaEventoAccion.CierreNoAbierto, (await HistoriaAsync(familiaId))[^1]);
    }

    [Fact]
    public async Task Una_regla_de_aviso_de_familia_encola_su_aviso_con_el_resumen()
    {
        var e = await CrearEntornoAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var conexion = new Conexion { Nombre = $"Falsa {Guid.NewGuid():N}", Tipo = "falso", ConfiguracionJson = "{}", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            db.Add(conexion);
            db.Add(new ReglaDeNotificacion
            {
                FlujoId = e.FlujoId, Nombre = "Lote terminado", Disparador = DisparadorDeNotificacion.FamiliaTerminada, Codigo = "Lote",
                ConexionId = conexion.Id, Destinatarios = ["jorge@example.com"],
                Asunto = "{caso}: {datos.completados} de {datos.total}", Cuerpo = "Tipo {tipo}", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var familiaId = await IdAsync(await e.Admin.PostAsJsonAsync("/api/v1/familias", new
        {
            flujoId = e.FlujoId, tipoDeFamiliaId = e.TipoId, nombre = "Avisada", participantesEsperados = (int?)null, casoIds = new[] { await CrearCasoAsync(e) },
        }));
        await e.Admin.PostAsync($"/api/v1/familias/{familiaId}/cerrar", null);
        await TrabajarAsync(e);
        await VueltaAsync();

        using var lectura = factory.Services.CreateScope();
        var envio = await lectura.ServiceProvider.GetRequiredService<AppDbContext>().Set<EnvioDeNotificacion>().AsNoTracking()
            .SingleAsync(x => x.FamiliaId == familiaId);
        Assert.Null(envio.CasoId);
        Assert.Equal("Avisada: 1 de 1", envio.Asunto);
        Assert.Equal("Tipo Lote", envio.Cuerpo);
    }

    [Fact]
    public async Task Acciones_masivas_anaden_casos_y_un_robot_no_usa_familias_de_otro_proceso()
    {
        var e = await CrearEntornoAsync();
        var otro = await CrearEntornoAsync();
        var casoId = await CrearCasoAsync(e);
        var familiaId = await IdAsync(await e.Admin.PostAsJsonAsync("/api/v1/familias", new
        {
            flujoId = e.FlujoId, tipoDeFamiliaId = e.TipoId, nombre = "Manual", participantesEsperados = (int?)null, casoIds = Array.Empty<Guid>(),
        }));

        var accion = await e.Admin.PostAsJsonAsync("/api/v1/casos/acciones/anadir-a-familia", new { ids = new[] { casoId }, parametros = new { familiaId } });
        Assert.True(accion.StatusCode == HttpStatusCode.OK, await accion.Content.ReadAsStringAsync());
        Assert.Equal(1, (await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/familias/{familiaId}")).GetProperty("miembros").GetInt32());

        var ajeno = Robot(otro.KeyLanzador);
        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => ajeno.CrearCasoAsync(new CrearCasoRobotRequest("x", null, Familias: [familiaId])));
        Assert.True(ex.IsConflict);
        Assert.True((await Assert.ThrowsAsync<ViriatoApiException>(() => ajeno.ObtenerFamiliaAsync(familiaId))).IsNotFound);
        Assert.True((await Assert.ThrowsAsync<ViriatoApiException>(() => ajeno.CerrarFamiliaAsync(familiaId))).IsNotFound);
    }
}
