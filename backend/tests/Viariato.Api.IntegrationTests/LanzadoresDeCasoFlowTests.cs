using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Despacho;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Casos.Lanzadores;
using Viariato.Modules.Flujos.Domain;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Authorization;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>
/// Launchers end to end: the author of a process says, with a crontab, when a creator fires by itself; at that moment the platform opens the
/// case — with the creator's type, step, estado and title and the launcher's data — and never opens the same moment twice.
/// </summary>
public sealed class LanzadoresDeCasoFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private const string EsquemaLote = """
        { "type": "object", "required": ["ticker"],
          "properties": { "ticker": { "type": "string", "title": "Valor" } } }
        """;

    private sealed record Entorno(HttpClient Admin, Guid FlujoId, Guid CreadorId);

    private async Task<HttpClient> AdminAsync(string? rol = SystemRoles.Admin)
    {
        var client = factory.CreateClient();
        var email = $"lanzador-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "SuperSecret123", displayName = "Prueba" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("id").GetGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RemoveRange(await db.Set<UserRole>().Where(ur => ur.UserId == userId).ToListAsync());
            if (rol is not null)
            {
                var role = await db.Set<Role>().SingleAsync(r => r.Name == rol);
                db.Add(new UserRole { UserId = userId, RoleId = role.Id, GrantedAt = DateTimeOffset.UtcNow });
            }

            await db.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "SuperSecret123" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Entorno> CrearEntornoAsync()
    {
        // The tests share one database and a turn of the clock looks at every launcher in it: the ones other tests left are paused.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Set<LanzadorDeCaso>().ExecuteUpdateAsync(set => set.SetProperty(l => l.Activo, false));
        }

        var admin = await AdminAsync();
        var flujoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Lote {Guid.NewGuid():N}", descripcion = (string?)null }));
        var versionId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new[]
            {
                new { orden = 1, nombre = "Validar", tipoPaso = "Interno", agenteDefinicionId = (Guid?)null, configuracionJson = (string?)null },
                new { orden = 2, nombre = "Analizar", tipoPaso = "Interno", agenteDefinicionId = (Guid?)null, configuracionJson = (string?)null },
            },
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null)).StatusCode);

        var tipoId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/tipos-caso", new { nombre = "Valor", orden = 1 }));
        var formularioId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/formularios", new { nombre = "Valor", esquemaJson = EsquemaLote }));
        var estadoId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/estados", new { codigo = "PENDIENTE", display = "Pendiente", orden = 1, esFinal = false }));
        var creadorId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/creadores", new
        {
            nombre = "Analizar un valor", descripcion = (string?)null, tipoCasoId = tipoId, formularioId, pasoInicialNombre = "Analizar",
            estadoNegocioInicialId = estadoId, plantillaTitulo = "{datos.ticker} {fecha} {hora}", orden = 1,
        }));

        return new Entorno(admin, flujoId, creadorId);
    }

    private static object Cuerpo(Guid creadorId, string nombre = "Cada hora", string cron = "0 * * * *", string zona = "UTC", string datos = """{"ticker":"SPY"}""", int tolerancia = 120) =>
        new { nombre, descripcion = (string?)null, creadorDeCasoId = creadorId, datosJson = datos, cron, zonaHoraria = zona, toleranciaMinutos = tolerancia, activo = true };

    private VigilanteDeLanzadores Vigilante() =>
        new(factory.Services.GetRequiredService<IServiceScopeFactory>(), Options.Create(new DespachoOptions()), NullLogger<VigilanteDeLanzadores>.Instance);

    private async Task PonerUltimoDisparoAsync(Guid lanzadorId, DateTimeOffset? cuando)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lanzador = await db.Set<LanzadorDeCaso>().SingleAsync(l => l.Id == lanzadorId);
        lanzador.UltimoDisparoEn = cuando;
        await db.SaveChangesAsync();
    }

    private async Task<List<Caso>> CasosDelFlujoAsync(Guid flujoId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Set<Caso>().AsNoTracking().Where(c => c.FlujoId == flujoId).OrderBy(c => c.CreatedAt).ToListAsync();
    }

    [Fact]
    public async Task UnLanzador_SeGuarda_ConSuProximoMomento_YNoDisparaLoQueYaPaso()
    {
        var e = await CrearEntornoAsync();

        var respuesta = await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores", Cuerpo(e.CreadorId));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var lanzador = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Analizar un valor", lanzador.GetProperty("creadorNombre").GetString());
        Assert.True(lanzador.GetProperty("proximoDisparoEn").GetDateTimeOffset() > DateTimeOffset.UtcNow);

        // Created now: the moments that went by before are not fired.
        Assert.Equal(0, await Vigilante().UnaVueltaAsync(DateTimeOffset.UtcNow, default));
        Assert.Empty(await CasosDelFlujoAsync(e.FlujoId));

        var lista = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/lanzadores");
        Assert.Single(lista.EnumerateArray());
    }

    [Theory]
    [InlineData("esto no es un cron", "UTC", """{"ticker":"SPY"}""")]
    [InlineData("0 25 * * *", "UTC", """{"ticker":"SPY"}""")]
    [InlineData("0 10 * * *", "Mars/Olympus", """{"ticker":"SPY"}""")]
    [InlineData("0 10 * * *", "UTC", "no es json")]
    public async Task UnLanzadorInvalido_SeRechazaConElMotivo(string cron, string zona, string datos)
    {
        var e = await CrearEntornoAsync();

        var respuesta = await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores", Cuerpo(e.CreadorId, cron: cron, zona: zona, datos: datos));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task LosDatos_TienenQueEncajarConElFormularioDelCreador()
    {
        var e = await CrearEntornoAsync();

        var respuesta = await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores", Cuerpo(e.CreadorId, datos: "{}"));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task ElCreador_TieneQueSerDelMismoProceso()
    {
        var e = await CrearEntornoAsync();
        var otro = await CrearEntornoAsync();

        var respuesta = await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores", Cuerpo(otro.CreadorId));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnUsuarioSinPermiso_NoPuedeCrearLanzadores()
    {
        var e = await CrearEntornoAsync();
        var sinPermiso = await AdminAsync(rol: null);

        var respuesta = await sinPermiso.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores", Cuerpo(e.CreadorId));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task CuandoLlegaSuMomento_AbreElCasoConTodoLoConfigurado_UnaSolaVez()
    {
        var e = await CrearEntornoAsync();
        var id = await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores", Cuerpo(e.CreadorId, cron: "30 14 * * *")));
        var momento = new DateTimeOffset(2030, 1, 15, 14, 30, 0, TimeSpan.Zero);
        await PonerUltimoDisparoAsync(id, momento.AddDays(-1));

        // 14:31 — a minute after its moment.
        var abiertos = await Vigilante().UnaVueltaAsync(momento.AddMinutes(1), default);

        Assert.Equal(1, abiertos);
        var caso = Assert.Single(await CasosDelFlujoAsync(e.FlujoId));
        Assert.Equal("SPY 2030-01-15 14:30", caso.Titulo);
        Assert.Null(caso.CreatedByUserId);
        Assert.Equal("SPY", JsonDocument.Parse(caso.DatosJson!).RootElement.GetProperty("ticker").GetString());
        Assert.NotNull(caso.EstadoNegocioActualId);

        var lanzador = (await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/lanzadores")).EnumerateArray().Single();
        Assert.Equal(momento, lanzador.GetProperty("ultimoDisparoEn").GetDateTimeOffset());
        Assert.Equal(caso.Id, lanzador.GetProperty("ultimoCasoId").GetGuid());
        Assert.Equal(JsonValueKind.Null, lanzador.GetProperty("ultimoError").ValueKind);

        // Looking again — also after a "restart", which is just another turn — does not repeat the moment.
        Assert.Equal(0, await Vigilante().UnaVueltaAsync(momento.AddMinutes(2), default));
        Assert.Single(await CasosDelFlujoAsync(e.FlujoId));
    }

    [Fact]
    public async Task UnMomentoMuyPasado_SeSalta_ElDeLaToleranciaNo()
    {
        var e = await CrearEntornoAsync();
        var id = await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores", Cuerpo(e.CreadorId, cron: "0 10 * * *", tolerancia: 60)));
        var momento = new DateTimeOffset(2030, 1, 15, 10, 0, 0, TimeSpan.Zero);
        await PonerUltimoDisparoAsync(id, momento.AddDays(-1));

        Assert.Equal(0, await Vigilante().UnaVueltaAsync(momento.AddMinutes(61), default));
        Assert.Empty(await CasosDelFlujoAsync(e.FlujoId));

        Assert.Equal(1, await Vigilante().UnaVueltaAsync(momento.AddMinutes(59), default));
        Assert.Single(await CasosDelFlujoAsync(e.FlujoId));
    }

    [Fact]
    public async Task VariosMomentosPerdidos_AbrenUnSoloCaso()
    {
        var e = await CrearEntornoAsync();
        var id = await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores", Cuerpo(e.CreadorId, cron: "*/10 * * * *")));
        var ahora = new DateTimeOffset(2030, 1, 15, 12, 35, 0, TimeSpan.Zero);
        await PonerUltimoDisparoAsync(id, ahora.AddHours(-1));

        Assert.Equal(1, await Vigilante().UnaVueltaAsync(ahora, default));

        Assert.Single(await CasosDelFlujoAsync(e.FlujoId));
        var lanzador = (await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/lanzadores")).EnumerateArray().Single();
        Assert.Equal(new DateTimeOffset(2030, 1, 15, 12, 30, 0, TimeSpan.Zero), lanzador.GetProperty("ultimoDisparoEn").GetDateTimeOffset());
    }

    [Fact]
    public async Task LaHoraSeLeeEnLaZonaDelLanzador()
    {
        var e = await CrearEntornoAsync();
        // 10:00 in New York is 15:00 UTC in January.
        var id = await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores", Cuerpo(e.CreadorId, cron: "0 10 * * 1-5", zona: "America/New_York")));
        await PonerUltimoDisparoAsync(id, new DateTimeOffset(2030, 1, 14, 0, 0, 0, TimeSpan.Zero));

        // Tuesday 2030-01-15, 10:01 in New York is 15:01 UTC (not 10:01 UTC).
        Assert.Equal(0, await Vigilante().UnaVueltaAsync(new DateTimeOffset(2030, 1, 15, 10, 1, 0, TimeSpan.Zero), default));
        Assert.Equal(1, await Vigilante().UnaVueltaAsync(new DateTimeOffset(2030, 1, 15, 15, 1, 0, TimeSpan.Zero), default));
        Assert.Equal("SPY 2030-01-15 10:00", Assert.Single(await CasosDelFlujoAsync(e.FlujoId)).Titulo);
    }

    [Fact]
    public async Task SiNoPuedeAbrirElCaso_LoDiceEnElLanzador_YNoLoReintentaCadaSegundo()
    {
        var e = await CrearEntornoAsync();
        var id = await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores", Cuerpo(e.CreadorId, cron: "0 10 * * *")));
        var momento = new DateTimeOffset(2030, 1, 15, 10, 0, 0, TimeSpan.Zero);
        await PonerUltimoDisparoAsync(id, momento.AddDays(-1));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Set<CreadorDeCaso>().SingleAsync(c => c.Id == e.CreadorId)).Activo = false;
            await db.SaveChangesAsync();
        }

        Assert.Equal(0, await Vigilante().UnaVueltaAsync(momento.AddMinutes(1), default));

        Assert.Empty(await CasosDelFlujoAsync(e.FlujoId));
        var lanzador = (await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/lanzadores")).EnumerateArray().Single();
        Assert.Contains("inactivo", lanzador.GetProperty("ultimoError").GetString());
        Assert.Equal(momento, lanzador.GetProperty("ultimoDisparoEn").GetDateTimeOffset());
    }

    [Fact]
    public async Task UnLanzadorPausado_NoDispara_YAlReactivarloNoDisparaLoQuePaso()
    {
        var e = await CrearEntornoAsync();
        var id = await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores", Cuerpo(e.CreadorId, cron: "* * * * *")));
        var pausado = new { nombre = "Cada hora", descripcion = (string?)null, creadorDeCasoId = e.CreadorId, datosJson = """{"ticker":"SPY"}""", cron = "* * * * *", zonaHoraria = "UTC", toleranciaMinutos = 120, activo = false };
        Assert.Equal(HttpStatusCode.OK, (await e.Admin.PutAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores/{id}", pausado)).StatusCode);
        await PonerUltimoDisparoAsync(id, DateTimeOffset.UtcNow.AddHours(-1));

        Assert.Equal(0, await Vigilante().UnaVueltaAsync(DateTimeOffset.UtcNow, default));

        Assert.Equal(HttpStatusCode.OK, (await e.Admin.PutAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores/{id}", pausado with { activo = true })).StatusCode);
        Assert.Equal(0, await Vigilante().UnaVueltaAsync(DateTimeOffset.UtcNow, default));
        Assert.Empty(await CasosDelFlujoAsync(e.FlujoId));
    }

    [Fact]
    public async Task UnLanzador_SeEditaYSeElimina()
    {
        var e = await CrearEntornoAsync();
        var id = await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores", Cuerpo(e.CreadorId)));

        var editado = await e.Admin.PutAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores/{id}", Cuerpo(e.CreadorId, nombre: "Cada dos horas", cron: "0 */2 * * *"));
        Assert.Equal(HttpStatusCode.OK, editado.StatusCode);
        Assert.Equal("0 */2 * * *", (await editado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("cron").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await e.Admin.DeleteAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores/{id}")).StatusCode);
        Assert.Empty((await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/lanzadores")).EnumerateArray());
    }

    [Fact]
    public async Task UnaListaDeDatos_AbreUnCasoPorElemento_CadaUnoConSuTitulo()
    {
        var e = await CrearEntornoAsync();
        var lista = """[{"ticker":"AAPL"},{"ticker":"MSFT"},{"ticker":"SPY"}]""";
        var id = await IdAsync(await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores", Cuerpo(e.CreadorId, cron: "30 14 * * *", datos: lista)));
        var momento = new DateTimeOffset(2030, 1, 15, 14, 30, 0, TimeSpan.Zero);
        await PonerUltimoDisparoAsync(id, momento.AddDays(-1));

        var abiertos = await Vigilante().UnaVueltaAsync(momento.AddMinutes(1), default);

        Assert.Equal(3, abiertos);
        var casos = await CasosDelFlujoAsync(e.FlujoId);
        Assert.Equal(["AAPL 2030-01-15 14:30", "MSFT 2030-01-15 14:30", "SPY 2030-01-15 14:30"], casos.Select(c => c.Titulo).Order().ToList());
        Assert.All(casos, c => Assert.Null(c.CreatedByUserId));
        var lanzador = (await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{e.FlujoId}/lanzadores")).EnumerateArray().Single();
        Assert.Equal(JsonValueKind.Null, lanzador.GetProperty("ultimoError").ValueKind);

        // The same moment is not opened again.
        Assert.Equal(0, await Vigilante().UnaVueltaAsync(momento.AddMinutes(2), default));
        Assert.Equal(3, (await CasosDelFlujoAsync(e.FlujoId)).Count);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""[{"ticker":"AAPL"}, 3]""")]
    [InlineData("3")]
    public async Task UnaListaInvalida_SeRechazaAlGuardar(string datos)
    {
        var e = await CrearEntornoAsync();

        var respuesta = await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores", Cuerpo(e.CreadorId, datos: datos));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task UnElementoDeLaListaQueNoEncajaConElFormulario_SeRechazaDiciendoCual()
    {
        var e = await CrearEntornoAsync();

        var respuesta = await e.Admin.PostAsJsonAsync($"/api/v1/flujos/{e.FlujoId}/lanzadores", Cuerpo(e.CreadorId, datos: """[{"ticker":"AAPL"},{}]"""));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("elemento 2", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task LosValoresPorDefectoDelCreador_SonLaBase_YElLanzadorPisaCampoACampo()
    {
        var e = await CrearEntornoAsync();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var creador = await db.Set<CreadorDeCaso>().SingleAsync(c => c.Id == e.CreadorId);
            creador.DatosPorDefectoJson = """{"ticker":"AAPL","nota":"del creador"}""";
            await db.SaveChangesAsync();
        }

        // The first element brings nothing of its own (the required ticker comes from the creator); the second changes only the ticker.
        var id = await IdAsync(await e.Admin.PostAsJsonAsync(
            $"/api/v1/flujos/{e.FlujoId}/lanzadores", Cuerpo(e.CreadorId, cron: "30 14 * * *", datos: """[{},{"ticker":"MSFT"}]""")));
        var momento = new DateTimeOffset(2030, 1, 15, 14, 30, 0, TimeSpan.Zero);
        await PonerUltimoDisparoAsync(id, momento.AddDays(-1));

        Assert.Equal(2, await Vigilante().UnaVueltaAsync(momento.AddMinutes(1), default));

        var casos = (await CasosDelFlujoAsync(e.FlujoId)).OrderBy(c => c.Titulo).ToList();
        Assert.Equal(["AAPL 2030-01-15 14:30", "MSFT 2030-01-15 14:30"], casos.Select(c => c.Titulo).ToList());
        Assert.All(casos, c => Assert.Contains("del creador", c.DatosJson));
        Assert.All(casos, c => Assert.NotNull(c.FormularioId));
    }
}
