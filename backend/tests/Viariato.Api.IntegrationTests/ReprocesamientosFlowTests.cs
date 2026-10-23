using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Reprocesamientos;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Authorization;
using Viriato.Rpa.Client;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>
/// Reprocessing rules: when a case ends in a business state and/or with its last step in some state, a step of the process runs again —
/// after a wait if the rule says so — up to the rule's limit, after which the case is left on the rule's final business state.
/// </summary>
public sealed class ReprocesamientosFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private sealed record Entorno(
        HttpClient Admin, Guid FlujoId, Guid PrimerPasoId, string Key, Dictionary<string, Guid> Estados);

    /// <summary>A process with two robot steps, «Preparar» and «Enviar», and the business states given.</summary>
    private async Task<Entorno> CrearEntornoAsync(params string[] estados)
    {
        var admin = factory.CreateClient();
        var email = $"reproceso-{Guid.NewGuid():N}@example.com";
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
        var servicioId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Portal {Guid.NewGuid():N}", descripcion = (string?)null }));
        var flujoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Pedidos {Guid.NewGuid():N}", descripcion = (string?)null }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/asignaciones", new { userId })).StatusCode);

        var ids = new Dictionary<string, Guid>();
        var orden = 0;
        foreach (var codigo in estados)
        {
            ids[codigo] = await IdAsync(await admin.PostAsJsonAsync(
                $"/api/v1/flujos/{flujoId}/estados", new { codigo, display = codigo, orden = ++orden, esFinal = false }));
        }

        var versionId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new object[]
            {
                new { orden = 1, nombre = "Preparar", tipoPaso = "Rpa", agenteDefinicionId = (Guid?)null, servicioId = (Guid?)servicioId, configuracionJson = "{\"aplicacion\":\"Portal\"}" },
                new { orden = 2, nombre = "Enviar", tipoPaso = "Rpa", agenteDefinicionId = (Guid?)null, servicioId = (Guid?)servicioId, configuracionJson = "{\"aplicacion\":\"Portal\"}" },
            },
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null)).StatusCode);
        var version = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{flujoId}/versiones/{versionId}");
        var primerPasoId = version.GetProperty("pasos")[0].GetProperty("id").GetGuid();

        var despliegue = await admin.PostAsJsonAsync("/api/v1/despliegues", new { equipoId, servicioId, flujoId });
        var key = (await despliegue.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("apiKey").GetString()!;
        return new Entorno(admin, flujoId, primerPasoId, key, ids);
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private RpaClient Robot(Entorno e)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", e.Key);
        return new RpaClient(http);
    }

    private async Task<Guid> CrearCasoAsync(Entorno e) => await IdAsync(await e.Admin.PostAsJsonAsync("/api/v1/casos", new
    {
        flujoId = e.FlujoId, titulo = $"Pedido {Guid.NewGuid():N}", pasoInicialId = e.PrimerPasoId,
    }));

    /// <summary>The robot takes the next step waiting in the queue, which must be of this case.</summary>
    private async Task<Guid> TomarAsync(Entorno e, Guid casoId)
    {
        var robot = Robot(e);
        await robot.ObtenerEstadoAsync();
        var asignada = await robot.ObtenerSiguienteEjecucionAsync();
        Assert.NotNull(asignada);
        Assert.Equal(casoId, asignada.CasoId);
        return asignada.EjecucionPasoId;
    }

    private Task<HttpResponseMessage> CrearReglaAsync(Entorno e, object regla) => e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/reprocesamientos", regla);

    private static object Regla(
        string nombre, Guid? estadoNegocioId = null, string? estadoEjecucion = null, string? paso = null, int espera = 0, int? limite = null,
        Guid? estadoFinalId = null, int orden = 0) => new
    {
        nombre, activa = true, orden, estadoNegocioId, estadoEjecucion, pasoNombre = paso, esperaMinutos = espera, limite, estadoFinalId,
    };

    private async Task<int> VueltaAsync(DateTimeOffset ahora)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ReprocesadorDeCasos>().UnaVueltaAsync(ahora, CancellationToken.None);
    }

    private async Task<Caso> CasoAsync(Guid casoId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Caso>().AsNoTracking().FirstAsync(c => c.Id == casoId);
    }

    private async Task<List<CasoEventoAccion>> HistoriaAsync(Guid casoId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<CasoEvento>().AsNoTracking()
            .Where(x => x.CasoId == casoId).OrderBy(x => x.OccurredAt).Select(x => x.Accion).ToListAsync();
    }

    private async Task<List<ReprocesamientoProgramado>> ProgramadosAsync(Guid casoId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<ReprocesamientoProgramado>().AsNoTracking()
            .Where(x => x.CasoId == casoId).OrderBy(x => x.Numero).ToListAsync();
    }

    [Fact]
    public async Task Un_paso_fallido_se_reprocesa_hasta_el_limite_y_el_caso_se_queda_en_el_estado_final()
    {
        var e = await CrearEntornoAsync("AGOTADO");
        Assert.Equal(HttpStatusCode.OK, (await CrearReglaAsync(e, Regla(
            "Reintentar fallos", estadoEjecucion: "Fallido", limite: 2, estadoFinalId: e.Estados["AGOTADO"]))).StatusCode);
        var casoId = await CrearCasoAsync(e);
        var robot = Robot(e);

        for (var intento = 1; intento <= 2; intento++)
        {
            await robot.FallarPasoAsync(await TomarAsync(e, casoId), "El portal no responde.");
            Assert.Equal(CasoEstado.Fallido, (await CasoAsync(casoId)).Estado);
            Assert.Equal(intento, (await ProgramadosAsync(casoId)).Count);

            Assert.Equal(1, await VueltaAsync(DateTimeOffset.UtcNow));
            Assert.Equal(CasoEstado.Pendiente, (await CasoAsync(casoId)).Estado);
        }

        // The third failure finds the limit spent: no third reprocessing, and the case stays failed on the final state.
        await robot.FallarPasoAsync(await TomarAsync(e, casoId), "El portal no responde.");
        var caso = await CasoAsync(casoId);
        Assert.Equal(CasoEstado.Fallido, caso.Estado);
        Assert.Equal(e.Estados["AGOTADO"], caso.EstadoNegocioActualId);
        Assert.Equal(2, (await ProgramadosAsync(casoId)).Count);
        Assert.All(await ProgramadosAsync(casoId), p => Assert.Equal(EstadoDeReprocesamiento.Hecho, p.Estado));
        Assert.Equal(0, await VueltaAsync(DateTimeOffset.UtcNow));

        var historia = await HistoriaAsync(casoId);
        Assert.Equal(2, historia.Count(a => a == CasoEventoAccion.ReprocesamientoProgramado));
        Assert.Equal(2, historia.Count(a => a == CasoEventoAccion.PasoReintentado));
        Assert.Contains(CasoEventoAccion.ReprocesamientosAgotados, historia);

        var historial = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/reprocesamientos/historial");
        Assert.Equal(2, historial.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Gana_la_primera_regla_que_encaja_y_espera_lo_que_dice()
    {
        var e = await CrearEntornoAsync("PORTAL_CAIDO");
        await CrearReglaAsync(e, Regla("Cualquier fallo", estadoEjecucion: "Fallido", orden: 2));
        Assert.Equal(HttpStatusCode.OK, (await CrearReglaAsync(e, Regla(
            "Portal caído", estadoNegocioId: e.Estados["PORTAL_CAIDO"], estadoEjecucion: "Fallido", espera: 30, orden: 1))).StatusCode);
        var casoId = await CrearCasoAsync(e);
        var robot = Robot(e);

        var paso = await TomarAsync(e, casoId);
        await robot.CambiarEstadoCasoAsync(paso, "PORTAL_CAIDO");
        await robot.FallarPasoAsync(paso, "Portal caído.");

        var programado = Assert.Single(await ProgramadosAsync(casoId));
        Assert.Equal("Portal caído", programado.ReglaNombre);

        Assert.Equal(0, await VueltaAsync(DateTimeOffset.UtcNow));
        Assert.Equal(CasoEstado.Fallido, (await CasoAsync(casoId)).Estado);
        Assert.Equal(1, await VueltaAsync(DateTimeOffset.UtcNow.AddMinutes(31)));
        Assert.Equal(CasoEstado.Pendiente, (await CasoAsync(casoId)).Estado);
    }

    [Fact]
    public async Task Se_reprocesa_el_paso_elegido_y_los_siguientes_vuelven_a_correr()
    {
        var e = await CrearEntornoAsync();
        Assert.Equal(HttpStatusCode.OK, (await CrearReglaAsync(e, Regla("Volver a preparar", estadoEjecucion: "Fallido", paso: "Preparar"))).StatusCode);
        var casoId = await CrearCasoAsync(e);
        var robot = Robot(e);

        await robot.CompletarPasoAsync(await TomarAsync(e, casoId));
        await robot.FallarPasoAsync(await TomarAsync(e, casoId), "No se pudo enviar.");
        Assert.Equal(1, await VueltaAsync(DateTimeOffset.UtcNow));

        // «Preparar» again, and after it «Enviar» as its second attempt.
        await robot.CompletarPasoAsync(await TomarAsync(e, casoId));
        await robot.CompletarCasoAsync(await TomarAsync(e, casoId));
        Assert.Equal(CasoEstado.Completado, (await CasoAsync(casoId)).Estado);

        using var scope = factory.Services.CreateScope();
        var intentos = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<EjecucionPaso>().AsNoTracking()
            .Where(p => p.CasoId == casoId).OrderBy(p => p.CreatedAt).Select(p => new { p.NumeroIntento, p.Estado }).ToListAsync();
        Assert.Equal([1, 1, 2, 2], intentos.Select(i => i.NumeroIntento));
        Assert.Equal(EjecucionPasoEstado.Completado, intentos[^1].Estado);
    }

    [Fact]
    public async Task Un_caso_descartado_por_una_persona_no_se_reprocesa()
    {
        var e = await CrearEntornoAsync();
        await CrearReglaAsync(e, Regla("Cancelados", estadoEjecucion: "Cancelado"));
        var casoId = await CrearCasoAsync(e);

        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.PostAsync($"/api/v1/casos/{casoId}/cancelar", null)).StatusCode);
        Assert.Equal(CasoEstado.Cancelado, (await CasoAsync(casoId)).Estado);
        Assert.Empty(await ProgramadosAsync(casoId));
    }

    [Fact]
    public async Task Un_caso_que_alguien_relanzo_a_mano_no_se_reprocesa_dos_veces()
    {
        var e = await CrearEntornoAsync();
        await CrearReglaAsync(e, Regla("Reintentar", estadoEjecucion: "Fallido", espera: 10));
        var casoId = await CrearCasoAsync(e);
        var paso = await TomarAsync(e, casoId);
        await Robot(e).FallarPasoAsync(paso, "Error.");

        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.PostAsync($"/api/v1/casos/{casoId}/pasos/{paso}/reprocesar", null)).StatusCode);
        Assert.Equal(0, await VueltaAsync(DateTimeOffset.UtcNow.AddMinutes(11)));
        Assert.Equal(EstadoDeReprocesamiento.Descartado, Assert.Single(await ProgramadosAsync(casoId)).Estado);
    }

    [Fact]
    public async Task La_regla_pide_al_menos_un_estado_y_con_limite_el_estado_final()
    {
        var e = await CrearEntornoAsync("FIN");
        var sinCondicion = await CrearReglaAsync(e, Regla("Nada"));
        Assert.Equal(HttpStatusCode.BadRequest, sinCondicion.StatusCode);
        Assert.Contains("estadoNegocioId", await sinCondicion.Content.ReadAsStringAsync());

        var sinFinal = await CrearReglaAsync(e, Regla("Sin final", estadoEjecucion: "Fallido", limite: 3));
        Assert.Equal(HttpStatusCode.BadRequest, sinFinal.StatusCode);
        Assert.Contains("estadoFinalId", await sinFinal.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, (await CrearReglaAsync(e, Regla("Raro", estadoEjecucion: "EnProgreso"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await CrearReglaAsync(e, Regla("Paso raro", estadoEjecucion: "Fallido", paso: "No existe"))).StatusCode);

        var pasos = await e.Admin.GetFromJsonAsync<List<string>>($"/api/v1/flujos/{e.FlujoId}/reprocesamientos/pasos");
        Assert.Equal(["Preparar", "Enviar"], pasos);
    }
}
