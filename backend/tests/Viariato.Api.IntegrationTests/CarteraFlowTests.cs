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

/// <summary>The simulated portfolio of the trading robots, kept in Viriato: robots write with their key, people read with a permission.</summary>
public sealed class CarteraFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private async Task<HttpClient> AdminAsync()
    {
        var client = factory.CreateClient();
        var email = $"cartera-{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "SuperSecret123", displayName = "Prueba" });
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("id").GetGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RemoveRange(await db.Set<UserRole>().Where(ur => ur.UserId == userId).ToListAsync());
            var admin = await db.Set<Role>().SingleAsync(r => r.Name == SystemRoles.Admin);
            db.Add(new UserRole { UserId = userId, RoleId = admin.Id, GrantedAt = DateTimeOffset.UtcNow });
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

    /// <summary>A robot: a client that only carries the key of a Despliegue.</summary>
    private async Task<HttpClient> RobotAsync(HttpClient admin)
    {
        var equipoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/equipos", new { nombre = $"Equipo {Guid.NewGuid():N}", descripcion = (string?)null }));
        var servicioId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Servicio {Guid.NewGuid():N}", descripcion = (string?)null }));
        var flujoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Proceso {Guid.NewGuid():N}", descripcion = (string?)null }));
        var despliegue = await admin.PostAsJsonAsync("/api/v1/despliegues", new { equipoId, servicioId, flujoId });
        Assert.Equal(HttpStatusCode.OK, despliegue.StatusCode);
        var clave = (await despliegue.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("apiKey").GetString();

        var robot = factory.CreateClient();
        robot.DefaultRequestHeaders.Add("X-Api-Key", clave);
        return robot;
    }

    private static object Compra(string caso, string ticker = "SPY", decimal cantidad = 0.1m, decimal precio = 200m, decimal? stop = 190m) =>
        new { casoId = caso, ticker, lado = "Compra", cantidad, precioUsd = precio, eurUsd = 1.0m, comisionPorcentaje = 0.1m, stopUsd = stop, capitalInicialEur = 100m };

    private static object Venta(string caso, string ticker = "SPY", decimal cantidad = 0.1m, decimal precio = 250m) =>
        new { casoId = caso, ticker, lado = "Venta", cantidad, precioUsd = precio, eurUsd = 1.0m, comisionPorcentaje = 0.1m, stopUsd = (decimal?)null, capitalInicialEur = 100m };

    [Fact]
    public async Task ElRobotCompraYVende_LaCarteraSeVeEnPantalla_YRepetirUnCasoNoOperaDosVeces()
    {
        var admin = await AdminAsync();
        var robot = await RobotAsync(admin);

        // The first time a robot asks, the portfolio is created with the capital its process says.
        var inicial = await robot.GetFromJsonAsync<JsonElement>("/api/v1/rpa/cartera?capitalInicialEur=100");
        Assert.Equal(100m, inicial.GetProperty("capitalInicialEur").GetDecimal());
        Assert.Equal(100m, inicial.GetProperty("efectivoEur").GetDecimal());
        Assert.Empty(inicial.GetProperty("posiciones").EnumerateArray());

        var compra = await robot.PostAsJsonAsync("/api/v1/rpa/cartera/movimientos", Compra("caso-1"));
        Assert.Equal(HttpStatusCode.OK, compra.StatusCode);
        var movimiento = await compra.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(movimiento.GetProperty("yaEstaba").GetBoolean());
        Assert.Equal(20m, movimiento.GetProperty("movimiento").GetProperty("importeEur").GetDecimal());
        Assert.Equal(0.02m, movimiento.GetProperty("movimiento").GetProperty("comisionEur").GetDecimal());

        // The same case again: nothing is bought twice.
        var otraVez = await (await robot.PostAsJsonAsync("/api/v1/rpa/cartera/movimientos", Compra("caso-1"))).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(otraVez.GetProperty("yaEstaba").GetBoolean());

        var estado = await robot.GetFromJsonAsync<JsonElement>("/api/v1/rpa/cartera");
        Assert.Equal(79.98m, estado.GetProperty("efectivoEur").GetDecimal());
        var posicion = Assert.Single(estado.GetProperty("posiciones").EnumerateArray());
        Assert.Equal(0.1m, posicion.GetProperty("cantidad").GetDecimal());
        Assert.Equal(190m, posicion.GetProperty("stopUsd").GetDecimal());

        // What people see: valued at the last price the robots saw.
        Assert.Equal(HttpStatusCode.NoContent, (await robot.PutAsJsonAsync("/api/v1/rpa/cartera/cotizaciones/SPY", new { precioUsd = 300m, eurUsd = 1.0m })).StatusCode);
        var pantalla = await admin.GetFromJsonAsync<JsonElement>("/api/v1/cartera");
        Assert.Equal(79.98m, pantalla.GetProperty("efectivoEur").GetDecimal());
        Assert.Equal(30m, pantalla.GetProperty("valorPosicionesEur").GetDecimal());
        Assert.Equal(109.98m, pantalla.GetProperty("patrimonioEur").GetDecimal());
        Assert.Equal(9.98m, pantalla.GetProperty("rentabilidadEur").GetDecimal());
        var valorada = Assert.Single(pantalla.GetProperty("posiciones").EnumerateArray());
        Assert.Equal(300m, valorada.GetProperty("precioActualUsd").GetDecimal());
        Assert.False(valorada.GetProperty("precioEsElCoste").GetBoolean());
        Assert.Equal(10m, valorada.GetProperty("gananciaEur").GetDecimal());
        Assert.Equal("caso-1", Assert.Single(pantalla.GetProperty("movimientos").EnumerateArray()).GetProperty("casoId").GetString());

        // Selling closes the position and gives back the cash minus the fee (0,1 % of 25 € rounds to 0,03).
        Assert.Equal(HttpStatusCode.OK, (await robot.PostAsJsonAsync("/api/v1/rpa/cartera/movimientos", Venta("caso-2"))).StatusCode);
        var cerrada = await admin.GetFromJsonAsync<JsonElement>("/api/v1/cartera");
        Assert.Empty(cerrada.GetProperty("posiciones").EnumerateArray());
        Assert.Equal(104.95m, cerrada.GetProperty("efectivoEur").GetDecimal());
        Assert.Equal(2, cerrada.GetProperty("movimientos").GetArrayLength());
    }

    [Fact]
    public async Task LoQueNoSePuedeHacer_SeRechazaConUnMotivo()
    {
        var admin = await AdminAsync();
        var robot = await RobotAsync(admin);

        var sinEfectivo = await robot.PostAsJsonAsync("/api/v1/rpa/cartera/movimientos", Compra("grande", ticker: "AAA", cantidad: 100m, precio: 500m));
        Assert.Equal(HttpStatusCode.Conflict, sinEfectivo.StatusCode);
        Assert.Contains("No hay efectivo", await sinEfectivo.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.Conflict, (await robot.PostAsJsonAsync("/api/v1/rpa/cartera/movimientos", Venta("v-sin-posicion", ticker: "ZZZ"))).StatusCode);

        foreach (var malo in new object[]
        {
            Compra("x", ticker: "../etc"),
            Compra("x", cantidad: 0m),
            new { casoId = "", ticker = "SPY", lado = "Compra", cantidad = 1m, precioUsd = 1m, eurUsd = 1m, comisionPorcentaje = 0.1m, stopUsd = (decimal?)null, capitalInicialEur = 100m },
            new { casoId = "y", ticker = "SPY", lado = "Regalo", cantidad = 1m, precioUsd = 1m, eurUsd = 1m, comisionPorcentaje = 0.1m, stopUsd = (decimal?)null, capitalInicialEur = 100m },
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await robot.PostAsJsonAsync("/api/v1/rpa/cartera/movimientos", malo)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await robot.PutAsJsonAsync("/api/v1/rpa/cartera/cotizaciones/SPY", new { precioUsd = 0m, eurUsd = 1m })).StatusCode);
    }

    [Fact]
    public async Task ElPermisoYLaClave_SeExigen()
    {
        var admin = await AdminAsync();

        // Without a robot key the robot endpoints are closed (a person's token is not a robot's key).
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/rpa/cartera")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await admin.GetAsync("/api/v1/rpa/cartera")).StatusCode);

        // A user with no permission cannot see the portfolio; the User role (the default) can.
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/cartera")).StatusCode);
        var usuario = factory.CreateClient();
        var email = $"sin-permiso-{Guid.NewGuid():N}@example.com";
        var register = await usuario.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "SuperSecret123", displayName = "Normal" });
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("id").GetGuid();
        var login = await usuario.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "SuperSecret123" });
        usuario.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await usuario.GetAsync("/api/v1/cartera")).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.RemoveRange(await db.Set<UserRole>().Where(ur => ur.UserId == userId).ToListAsync());
            await db.SaveChangesAsync();
        }

        var sinRol = factory.CreateClient();
        var login2 = await sinRol.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "SuperSecret123" });
        sinRol.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await sinRol.GetAsync("/api/v1/cartera")).StatusCode);
    }
}
