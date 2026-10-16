using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Authorization;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>What the trading robots learn from: the analyses they register, how the price judged them, and the changes to the weights of the rules they propose.</summary>
public sealed class CarteraAprendizajeFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private async Task<HttpClient> PersonaAsync(string rol)
    {
        var client = factory.CreateClient();
        var email = $"aprende-{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "SuperSecret123", displayName = "Prueba" });
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("id").GetGuid();

        if (rol == SystemRoles.Admin)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RemoveRange(await db.Set<UserRole>().Where(ur => ur.UserId == userId).ToListAsync());
            var admin = await db.Set<Role>().SingleAsync(r => r.Name == SystemRoles.Admin);
            db.Add(new UserRole { UserId = userId, RoleId = admin.Id, GrantedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "SuperSecret123" });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());
        return client;
    }

    private async Task<HttpClient> RobotAsync(HttpClient admin)
    {
        static async Task<Guid> Id(HttpResponseMessage r)
        {
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        var equipoId = await Id(await admin.PostAsJsonAsync("/api/v1/equipos", new { nombre = $"Equipo {Guid.NewGuid():N}", descripcion = (string?)null }));
        var servicioId = await Id(await admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Servicio {Guid.NewGuid():N}", descripcion = (string?)null }));
        var flujoId = await Id(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Proceso {Guid.NewGuid():N}", descripcion = (string?)null }));
        var despliegue = await admin.PostAsJsonAsync("/api/v1/despliegues", new { equipoId, servicioId, flujoId });
        var clave = (await despliegue.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("apiKey").GetString();

        var robot = factory.CreateClient();
        robot.DefaultRequestHeaders.Add("X-Api-Key", clave);
        return robot;
    }

    private static object Senal(string caso, string ticker, string senal, int dias, decimal precio = 100m, string? orden = null, string votos = "{\"tendencia\":2,\"rsi\":0,\"macd\":1,\"bollinger\":0}") =>
        new { casoId = caso, ticker, fecha = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-dias).ToString("yyyy-MM-dd"), precioUsd = precio, senal, puntos = 3m, votosJson = votos, senalIa = "comprar", confianzaIa = 70, ordenLado = orden };

    [Fact]
    public async Task UnaSenal_SeRegistra_SeEvalua_YSeVeEnLasEstadisticas()
    {
        var admin = await PersonaAsync(SystemRoles.Admin);
        var robot = await RobotAsync(admin);

        // Two buys and a hold from 10 days ago, one buy from today (too recent to judge).
        foreach (var s in new[] { Senal("a1", "AAA", "Comprar", 10), Senal("a2", "BBB", "Comprar", 10), Senal("a3", "CCC", "Mantener", 10), Senal("a4", "DDD", "Comprar", 0) })
        {
            var r = await robot.PostAsJsonAsync("/api/v1/rpa/cartera/senales", s);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.False((await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("yaEstaba").GetBoolean());
        }

        // Registering the same analysis again changes nothing.
        Assert.True((await (await robot.PostAsJsonAsync("/api/v1/rpa/cartera/senales", Senal("a1", "AAA", "Comprar", 10))).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("yaEstaba").GetBoolean());

        var pendientes = await robot.GetFromJsonAsync<JsonElement>("/api/v1/rpa/cartera/senales/por-evaluar?dias=5");
        Assert.Equal(3, pendientes.GetArrayLength()); // the one of today is not due yet

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        Assert.Equal(HttpStatusCode.NoContent, (await robot.PutAsJsonAsync("/api/v1/rpa/cartera/senales/a1/evaluacion", new { precioUsd = 105m, fecha = hoy })).StatusCode); // +5 %: a hit
        Assert.Equal(HttpStatusCode.NoContent, (await robot.PutAsJsonAsync("/api/v1/rpa/cartera/senales/a2/evaluacion", new { precioUsd = 98m, fecha = hoy })).StatusCode); // -2 %: a miss
        Assert.Equal(HttpStatusCode.NoContent, (await robot.PutAsJsonAsync("/api/v1/rpa/cartera/senales/a3/evaluacion", new { precioUsd = 101m, fecha = hoy })).StatusCode); // hold, +1 %: a hit
        // The first judgement stands.
        Assert.Equal(HttpStatusCode.NoContent, (await robot.PutAsJsonAsync("/api/v1/rpa/cartera/senales/a1/evaluacion", new { precioUsd = 50m, fecha = hoy })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await robot.PutAsJsonAsync("/api/v1/rpa/cartera/senales/no-existe/evaluacion", new { precioUsd = 1m, fecha = hoy })).StatusCode);

        var evaluadas = await robot.GetFromJsonAsync<JsonElement>("/api/v1/rpa/cartera/senales/evaluadas");
        Assert.Equal(3, evaluadas.GetArrayLength());
        Assert.Contains(evaluadas.EnumerateArray(), e => e.GetProperty("ticker").GetString() == "AAA" && e.GetProperty("retornoPorcentaje").GetDecimal() == 5m);

        var pantalla = await admin.GetFromJsonAsync<JsonElement>("/api/v1/cartera/aprendizaje");
        Assert.Equal(3, pantalla.GetProperty("evaluadasTotal").GetInt32());
        var compras = pantalla.GetProperty("estadisticas").EnumerateArray().Single(e => e.GetProperty("senal").GetString() == "Comprar");
        Assert.Equal(2, compras.GetProperty("muestras").GetInt32());
        Assert.Equal(1, compras.GetProperty("aciertos").GetInt32());
        Assert.Equal(50.0, compras.GetProperty("aciertoPorcentaje").GetDouble());
        Assert.Equal(4, pantalla.GetProperty("recientes").GetArrayLength());
    }

    [Fact]
    public async Task UnaOrdenReciente_SeVe_YDejaDeVerseConElTiempo()
    {
        var admin = await PersonaAsync(SystemRoles.Admin);
        var robot = await RobotAsync(admin);

        Assert.Equal(HttpStatusCode.OK, (await robot.PostAsJsonAsync("/api/v1/rpa/cartera/senales", Senal("o1", "ORD", "Comprar", 0, orden: "Compra"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await robot.PostAsJsonAsync("/api/v1/rpa/cartera/senales", Senal("o2", "SINORD", "Comprar", 0))).StatusCode);

        var ordenes = await robot.GetFromJsonAsync<JsonElement>("/api/v1/rpa/cartera/ordenes-recientes?horas=24");
        var propia = Assert.Single(ordenes.EnumerateArray(), o => o.GetProperty("ticker").GetString() == "ORD");
        Assert.Equal("Compra", propia.GetProperty("lado").GetString());
        Assert.DoesNotContain(ordenes.EnumerateArray(), o => o.GetProperty("ticker").GetString() == "SINORD");
    }

    [Fact]
    public async Task LaPropuestaDePesos_SePropone_SeAplica_YEntraEnVigor_SoloConPermiso()
    {
        var admin = await PersonaAsync(SystemRoles.Admin);
        var robot = await RobotAsync(admin);

        var inicial = await robot.GetFromJsonAsync<JsonElement>("/api/v1/rpa/cartera/pesos");
        Assert.Equal(0, inicial.GetProperty("version").GetInt32());
        Assert.Equal(1.0, inicial.GetProperty("pesos").GetProperty("macd").GetDouble());

        // Out-of-range weights are refused.
        Assert.Equal(HttpStatusCode.BadRequest, (await robot.PostAsJsonAsync("/api/v1/rpa/cartera/pesos/propuesta",
            new { pesos = new Dictionary<string, double> { ["tendencia"] = 1, ["rsi"] = 1, ["macd"] = 3, ["bollinger"] = 1 }, muestras = 50, motivo = "x", detalleJson = (string?)null })).StatusCode);

        var propuesta = new { pesos = new Dictionary<string, double> { ["tendencia"] = 1, ["rsi"] = 0.8, ["macd"] = 1.2, ["bollinger"] = 1 }, muestras = 50, motivo = "pesan más macd", detalleJson = "[]" };
        Assert.Equal(HttpStatusCode.NoContent, (await robot.PostAsJsonAsync("/api/v1/rpa/cartera/pesos/propuesta", propuesta)).StatusCode);

        // A proposal is not in force until a person applies it.
        Assert.Equal(0, (await robot.GetFromJsonAsync<JsonElement>("/api/v1/rpa/cartera/pesos")).GetProperty("version").GetInt32());
        var pantalla = await admin.GetFromJsonAsync<JsonElement>("/api/v1/cartera/aprendizaje");
        var id = pantalla.GetProperty("propuesta").GetProperty("id").GetGuid();
        Assert.Equal(1.2, pantalla.GetProperty("propuesta").GetProperty("pesos").GetProperty("macd").GetDouble());

        // A newer proposal replaces the waiting one.
        Assert.Equal(HttpStatusCode.NoContent, (await robot.PostAsJsonAsync("/api/v1/rpa/cartera/pesos/propuesta", propuesta)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/v1/cartera/aprendizaje/propuesta/{id}/aplicar", null)).StatusCode);
        var nuevaId = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/cartera/aprendizaje")).GetProperty("propuesta").GetProperty("id").GetGuid();

        // A person without the permission cannot apply it.
        var usuario = await PersonaAsync("User");
        Assert.Equal(HttpStatusCode.OK, (await usuario.GetAsync("/api/v1/cartera/aprendizaje")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await usuario.PostAsync($"/api/v1/cartera/aprendizaje/propuesta/{nuevaId}/aplicar", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/cartera/aprendizaje/propuesta/{nuevaId}/aplicar", null)).StatusCode);
        var vigor = await robot.GetFromJsonAsync<JsonElement>("/api/v1/rpa/cartera/pesos");
        Assert.True(vigor.GetProperty("version").GetInt32() > 0);
        Assert.Equal(1.2, vigor.GetProperty("pesos").GetProperty("macd").GetDouble());
        Assert.Equal(JsonValueKind.Null, (await admin.GetFromJsonAsync<JsonElement>("/api/v1/cartera/aprendizaje")).GetProperty("propuesta").ValueKind);

        // And it can be turned down.
        Assert.Equal(HttpStatusCode.NoContent, (await robot.PostAsJsonAsync("/api/v1/rpa/cartera/pesos/propuesta", propuesta)).StatusCode);
        var otraId = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/cartera/aprendizaje")).GetProperty("propuesta").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/cartera/aprendizaje/propuesta/{otraId}/descartar", null)).StatusCode);
        Assert.Equal(JsonValueKind.Null, (await admin.GetFromJsonAsync<JsonElement>("/api/v1/cartera/aprendizaje")).GetProperty("propuesta").ValueKind);
    }

    private static object Modelo(bool aprobado, string motivo = "ok") => new
    {
        modeloJson = "{\"pesos\":[0.1,0.2]}",
        muestrasDeEntrenamiento = 800,
        muestrasDePrueba = 300,
        precision = 0.55,
        precisionBase = 0.52,
        senales = 40,
        retornoMedioConSenalPorcentaje = 0.4,
        retornoMedioGeneralPorcentaje = 0.1,
        aprobado,
        motivo,
    };

    [Fact]
    public async Task ElModeloReentrenado_SoloSustituyeAlAnteriorSiLaPuertaLoAprueba()
    {
        var admin = await PersonaAsync(SystemRoles.Admin);
        var robot = await RobotAsync(admin);

        // The tests of this class share one database: versions are counted from where they are now.
        var existentes = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/cartera/aprendizaje")).GetProperty("modelos").EnumerateArray().Select(m => m.GetProperty("version").GetInt32()).ToList();
        var @base = existentes.Count == 0 ? 0 : existentes.Max();

        Assert.Equal(HttpStatusCode.OK, (await robot.PostAsJsonAsync("/api/v1/rpa/cartera/modelos", Modelo(aprobado: true, "primero"))).StatusCode);
        Assert.Equal(@base + 1, (await robot.GetFromJsonAsync<JsonElement>("/api/v1/rpa/cartera/modelos/activo")).GetProperty("version").GetInt32());

        // A candidate that did not earn it is kept, but the one in force stays.
        Assert.Equal(HttpStatusCode.OK, (await robot.PostAsJsonAsync("/api/v1/rpa/cartera/modelos", Modelo(aprobado: false, "no supera a la base"))).StatusCode);
        Assert.Equal(@base + 1, (await robot.GetFromJsonAsync<JsonElement>("/api/v1/rpa/cartera/modelos/activo")).GetProperty("version").GetInt32());

        // One that does earn it takes over, and the old one becomes history.
        Assert.Equal(HttpStatusCode.OK, (await robot.PostAsJsonAsync("/api/v1/rpa/cartera/modelos", Modelo(aprobado: true, "tercero"))).StatusCode);
        Assert.Equal(@base + 3, (await robot.GetFromJsonAsync<JsonElement>("/api/v1/rpa/cartera/modelos/activo")).GetProperty("version").GetInt32());

        var pantalla = await admin.GetFromJsonAsync<JsonElement>("/api/v1/cartera/aprendizaje");
        Assert.Equal(@base + 3, pantalla.GetProperty("modeloActivo").GetProperty("version").GetInt32());
        var estados = pantalla.GetProperty("modelos").EnumerateArray().ToDictionary(m => m.GetProperty("version").GetInt32(), m => m.GetProperty("estado").GetString());
        Assert.Equal("Anterior", estados[@base + 1]);
        Assert.Equal("Rechazado", estados[@base + 2]);
        Assert.Equal("Activo", estados[@base + 3]);

        Assert.Equal(HttpStatusCode.BadRequest, (await robot.PostAsJsonAsync("/api/v1/rpa/cartera/modelos", new { modeloJson = "no es json", muestrasDeEntrenamiento = 1, muestrasDePrueba = 1, precision = 0.5, precisionBase = 0.5, senales = 0, retornoMedioConSenalPorcentaje = 0.0, retornoMedioGeneralPorcentaje = 0.0, aprobado = true, motivo = (string?)null })).StatusCode);
    }

    [Fact]
    public async Task ElModelo_SeJuzgaConLasSenalesQueHizoYaEnVigor()
    {
        var admin = await PersonaAsync(SystemRoles.Admin);
        var robot = await RobotAsync(admin);
        Assert.Equal(HttpStatusCode.OK, (await robot.PostAsJsonAsync("/api/v1/rpa/cartera/modelos", Modelo(aprobado: true))).StatusCode);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        object ConProbabilidad(string caso, decimal p) => new
        {
            casoId = caso, ticker = "ML" + caso, fecha = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-10).ToString("yyyy-MM-dd"), precioUsd = 100m, senal = "Mantener", puntos = 0m,
            votosJson = "{\"tendencia\":0,\"rsi\":0,\"macd\":0,\"bollinger\":0}", senalIa = (string?)null, confianzaIa = (int?)null, ordenLado = (string?)null, probabilidadMl = (decimal?)p,
        };

        await robot.PostAsJsonAsync("/api/v1/rpa/cartera/senales", ConProbabilidad("m1", 0.70m)); // says up
        await robot.PostAsJsonAsync("/api/v1/rpa/cartera/senales", ConProbabilidad("m2", 0.30m)); // says down
        await robot.PutAsJsonAsync("/api/v1/rpa/cartera/senales/m1/evaluacion", new { precioUsd = 103m, fecha = hoy }); // rose: right
        await robot.PutAsJsonAsync("/api/v1/rpa/cartera/senales/m2/evaluacion", new { precioUsd = 103m, fecha = hoy }); // rose: wrong

        var vivo = (await admin.GetFromJsonAsync<JsonElement>("/api/v1/cartera/aprendizaje")).GetProperty("modeloEnVivo");
        Assert.Equal(2, vivo.GetProperty("muestras").GetInt32());
        Assert.Equal(50.0, vivo.GetProperty("aciertoPorcentaje").GetDouble());
        Assert.Equal(1, vivo.GetProperty("senales").GetInt32());
        Assert.Equal(3.0, vivo.GetProperty("retornoMedioConSenalPorcentaje").GetDouble());
    }

    [Fact]
    public async Task UnaSenalMalFormada_SeRechaza()
    {
        var robot = await RobotAsync(await PersonaAsync(SystemRoles.Admin));

        foreach (var mala in new[]
        {
            Senal("m1", "../x", "Comprar", 0),
            Senal("m2", "AAA", "Quizás", 0),
            Senal("m3", "AAA", "Comprar", 0, votos: "{\"tendencia\":9,\"rsi\":0,\"macd\":0,\"bollinger\":0}"),
            Senal("m4", "AAA", "Comprar", 0, votos: "no es json"),
            Senal("m5", "AAA", "Comprar", 0, orden: "Regalo"),
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await robot.PostAsJsonAsync("/api/v1/rpa/cartera/senales", mala)).StatusCode);
        }
    }
}
