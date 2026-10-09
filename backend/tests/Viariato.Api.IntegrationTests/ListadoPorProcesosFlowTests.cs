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

/// <summary>The list of cases can be narrowed to several processes at once (the screen's process picker).</summary>
public sealed class ListadoPorProcesosFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private async Task<(HttpClient Client, Guid UserId)> AdminAsync()
    {
        var client = factory.CreateClient();
        var email = $"lista-{Guid.NewGuid():N}@example.com";
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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());
        return (client, userId);
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    /// <summary>A process with one case in it; returns both ids.</summary>
    private static async Task<(Guid Flujo, Guid Caso)> ProcesoConUnCasoAsync(HttpClient client, Guid userId, string titulo)
    {
        var flujo = await IdAsync(await client.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Proceso {Guid.NewGuid():N}", descripcion = (string?)null }));
        var version = await IdAsync(await client.PostAsJsonAsync($"/api/v1/flujos/{flujo}/versiones", new { notas = (string?)null }));
        await client.PutAsJsonAsync($"/api/v1/flujos/{flujo}/versiones/{version}/pasos", new
        {
            pasos = new[] { new { orden = 1, nombre = "Paso", tipoPaso = "Interno", agenteDefinicionId = (Guid?)null, configuracionJson = (string?)null } },
        });
        await client.PostAsync($"/api/v1/flujos/{flujo}/versiones/{version}/publicar", null);
        await client.PostAsJsonAsync($"/api/v1/flujos/{flujo}/asignaciones", new { userId });
        var caso = await IdAsync(await client.PostAsJsonAsync("/api/v1/casos", new { flujoId = flujo, flujoVersionId = (Guid?)null, titulo, datosJson = (string?)null }));
        return (flujo, caso);
    }

    private static async Task<HashSet<Guid>> IdsAsync(HttpClient client, string consulta)
    {
        var pagina = await client.GetFromJsonAsync<JsonElement>($"/api/v1/casos?pageSize=100&{consulta}");
        return pagina.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToHashSet();
    }

    [Fact]
    public async Task VariosProcesos_DevuelvenLosCasosDeEsosProcesos_YNadaMas()
    {
        var (client, userId) = await AdminAsync();
        var a = await ProcesoConUnCasoAsync(client, userId, "En A");
        var b = await ProcesoConUnCasoAsync(client, userId, "En B");
        var c = await ProcesoConUnCasoAsync(client, userId, "En C");

        var ab = await IdsAsync(client, $"flujoIds={a.Flujo},{b.Flujo}");
        var soloC = await IdsAsync(client, $"flujoIds={c.Flujo}");

        Assert.Equal([a.Caso, b.Caso], ab.Order());
        Assert.Equal([c.Caso], soloC);
    }

    [Fact]
    public async Task UnProcesoAjeno_EnLaLista_NoDevuelveSusCasos()
    {
        var (client, userId) = await AdminAsync();
        var propio = await ProcesoConUnCasoAsync(client, userId, "Propio");
        var (otro, otroId) = await AdminAsync();
        var ajeno = await ProcesoConUnCasoAsync(otro, otroId, "Ajeno");

        var ids = await IdsAsync(client, $"flujoIds={propio.Flujo},{ajeno.Flujo}");

        Assert.Equal([propio.Caso], ids);
    }

    [Fact]
    public async Task SinLista_OConTextoQueNoEsUnId_NoSeFiltraPorProceso()
    {
        var (client, userId) = await AdminAsync();
        var a = await ProcesoConUnCasoAsync(client, userId, "En A");
        var b = await ProcesoConUnCasoAsync(client, userId, "En B");

        var sin = await IdsAsync(client, "");
        var basura = await IdsAsync(client, "flujoIds=no-es-un-id,,");

        Assert.Contains(a.Caso, sin);
        Assert.Contains(b.Caso, sin);
        Assert.Equal(sin, basura);
    }
}
