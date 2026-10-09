using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Authorization;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>
/// The dashboard counts the Casos that are still moving plus the ones that finished inside a window. Opening a group
/// from the dashboard has to list exactly those, not every Caso in the group.
/// </summary>
public sealed class CasosVentanaFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private sealed record Entorno(HttpClient Admin, Guid FlujoId, Guid CasoEnCurso, Guid CasoDeHoy, Guid CasoDeAyer, Guid CasoDeHaceUnMes);

    private static readonly DateTimeOffset Ahora = DateTimeOffset.UtcNow;

    private async Task<Entorno> CrearEntornoAsync()
    {
        var admin = factory.CreateClient();
        var email = $"ventana-{Guid.NewGuid():N}@example.com";
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
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());

        var servicioId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Servicio {Guid.NewGuid():N}", descripcion = (string?)null }));
        var flujoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Proceso {Guid.NewGuid():N}", descripcion = (string?)null }));
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/asignaciones", new { userId })).StatusCode);
        var versionId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        var pasos = await admin.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new object[]
            {
                new { orden = 1, nombre = "Uno", tipoPaso = "Rpa", agenteDefinicionId = (Guid?)null, servicioId = (Guid?)servicioId, configuracionJson = "{\"aplicacion\":\"A\"}" },
            },
        });
        Assert.Equal(HttpStatusCode.OK, pasos.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null)).StatusCode);

        var enCurso = await NuevoCasoAsync(admin, flujoId, "En curso");
        var deHoy = await CancelarAsync(admin, await NuevoCasoAsync(admin, flujoId, "Finalizado hoy"));
        var deAyer = await CancelarAsync(admin, await NuevoCasoAsync(admin, flujoId, "Finalizado ayer"));
        var deHaceUnMes = await CancelarAsync(admin, await NuevoCasoAsync(admin, flujoId, "Finalizado hace un mes"));
        await FinalizadoEnAsync(deAyer, Ahora.AddDays(-1));
        await FinalizadoEnAsync(deHaceUnMes, Ahora.AddDays(-30));

        return new Entorno(admin, flujoId, enCurso, deHoy, deAyer, deHaceUnMes);
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> NuevoCasoAsync(HttpClient admin, Guid flujoId, string titulo) =>
        await IdAsync(await admin.PostAsJsonAsync("/api/v1/casos", new { flujoId, titulo }));

    private static async Task<Guid> CancelarAsync(HttpClient admin, Guid casoId)
    {
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/casos/{casoId}/cancelar", null)).StatusCode);
        return casoId;
    }

    private async Task FinalizadoEnAsync(Guid casoId, DateTimeOffset cuando)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var caso = await db.Set<Caso>().SingleAsync(c => c.Id == casoId);
        caso.CompletedAt = cuando;
        await db.SaveChangesAsync();
    }

    private static async Task<HashSet<Guid>> ListarAsync(Entorno e, string consulta)
    {
        var json = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos?flujoId={e.FlujoId}&pageSize=100&{consulta}");
        return json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToHashSet();
    }

    private static HashSet<Guid> Set(params Guid[] ids) => ids.ToHashSet();

    private static string Iso(DateTimeOffset fecha) => Uri.EscapeDataString(fecha.ToString("O"));

    [Fact]
    public async Task WithoutTheWindow_TheListStillHasEveryCaso()
    {
        var e = await CrearEntornoAsync();

        Assert.Equal(Set(e.CasoEnCurso, e.CasoDeHoy, e.CasoDeAyer, e.CasoDeHaceUnMes), await ListarAsync(e, ""));
    }

    [Fact]
    public async Task ByDefault_TheWindowIsToday_StillMovingCasosAlwaysIncluded()
    {
        var e = await CrearEntornoAsync();

        Assert.Equal(Set(e.CasoEnCurso, e.CasoDeHoy), await ListarAsync(e, "ventana=true"));
    }

    [Fact]
    public async Task TodosRemovesTheLimit()
    {
        var e = await CrearEntornoAsync();

        Assert.Equal(Set(e.CasoEnCurso, e.CasoDeHoy, e.CasoDeAyer, e.CasoDeHaceUnMes), await ListarAsync(e, "ventana=true&finalizados=todos"));
    }

    [Fact]
    public async Task ARange_IsAppliedToWhenTheCasoFinished_NotToWhenItWasCreated()
    {
        var e = await CrearEntornoAsync();

        // All four were created a moment ago; only the finish date tells them apart.
        var ayer = await ListarAsync(e, $"ventana=true&completadoDesde={Iso(Ahora.AddDays(-2))}&completadoHasta={Iso(Ahora.AddHours(-12))}");
        Assert.Equal(Set(e.CasoEnCurso, e.CasoDeAyer), ayer);

        var delMes = await ListarAsync(e, $"ventana=true&completadoDesde={Iso(Ahora.AddDays(-40))}&completadoHasta={Iso(Ahora.AddDays(-20))}");
        Assert.Equal(Set(e.CasoEnCurso, e.CasoDeHaceUnMes), delMes);

        var desdeAyer = await ListarAsync(e, $"ventana=true&completadoDesde={Iso(Ahora.AddDays(-2))}");
        Assert.Equal(Set(e.CasoEnCurso, e.CasoDeHoy, e.CasoDeAyer), desdeAyer);
    }

    [Fact]
    public async Task OpeningTheFinishedGroup_ListsOnlyTheFinishedOnesInTheWindow()
    {
        var e = await CrearEntornoAsync();

        Assert.Equal(Set(e.CasoDeHoy), await ListarAsync(e, "ventana=true&finalizado=true"));
        Assert.Equal(Set(e.CasoEnCurso), await ListarAsync(e, "ventana=true&finalizado=false"));
        Assert.Equal(
            Set(e.CasoDeHoy, e.CasoDeAyer, e.CasoDeHaceUnMes),
            await ListarAsync(e, "ventana=true&finalizado=true&finalizados=todos"));
    }

    [Fact]
    public async Task WhatTheDashboardCounts_IsWhatTheDrillDownLists()
    {
        var e = await CrearEntornoAsync();

        foreach (var (consulta, parametros) in new[]
        {
            ("", "ventana=true"),
            ("finalizados=todos", "ventana=true&finalizados=todos"),
            ($"desde={Iso(Ahora.AddDays(-2))}&hasta={Iso(Ahora.AddHours(-12))}", $"ventana=true&completadoDesde={Iso(Ahora.AddDays(-2))}&completadoHasta={Iso(Ahora.AddHours(-12))}"),
        })
        {
            var resumen = await e.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/casos/resumen?flujoId={e.FlujoId}&{consulta}");
            var contados = resumen[0].GetProperty("total").GetInt32();
            Assert.Equal(contados, (await ListarAsync(e, parametros)).Count);
        }
    }
}
