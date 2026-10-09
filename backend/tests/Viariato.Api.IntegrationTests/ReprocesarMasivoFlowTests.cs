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

/// <summary>The bulk action "reprocesar": failed Casos go back on their way, everything else is left alone and said so.</summary>
public sealed class ReprocesarMasivoFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private async Task<(HttpClient Client, Guid UserId)> AdminAsync()
    {
        var client = factory.CreateClient();
        var email = $"reproc-{Guid.NewGuid():N}@example.com";
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
        return (client, userId);
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<string?> EstadoAsync(HttpClient client, Guid casoId) =>
        (await client.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}")).GetProperty("estado").GetString();

    /// <summary>A case that fails: its only step is a human review, and the reviewer rejects it.</summary>
    private static async Task<Guid> CasoFallidoAsync(HttpClient client, Guid flujoId, string titulo)
    {
        var casoId = await IdAsync(await client.PostAsJsonAsync("/api/v1/casos", new { flujoId, flujoVersionId = (Guid?)null, titulo, datosJson = (string?)null }));
        var pendientes = await client.GetFromJsonAsync<JsonElement>("/api/v1/revisiones?estado=pendiente");
        var pasoId = pendientes.EnumerateArray().First(p => p.GetProperty("casoId").GetGuid() == casoId).GetProperty("ejecucionPasoId").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/api/v1/revisiones/{pasoId}/resolver", new { decision = "Rechazada", comentario = "mal" })).StatusCode);
        Assert.Equal("Fallido", await EstadoAsync(client, casoId));
        return casoId;
    }

    [Fact]
    public async Task LosCasosFallidos_VuelvenAEmpezar_YLosDemasSeDejanComoEstan()
    {
        var (client, userId) = await AdminAsync();
        var flujoId = await IdAsync(await client.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Proceso {Guid.NewGuid():N}", descripcion = (string?)null }));
        var versionId = await IdAsync(await client.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        await client.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new[] { new { orden = 1, nombre = "Revisar", tipoPaso = "RevisionHumana", agenteDefinicionId = (Guid?)null, configuracionJson = (string?)null } },
        });
        await client.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null);
        await client.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/asignaciones", new { userId });

        var fallido1 = await CasoFallidoAsync(client, flujoId, "Fallido uno");
        var fallido2 = await CasoFallidoAsync(client, flujoId, "Fallido dos");
        var activo = await IdAsync(await client.PostAsJsonAsync("/api/v1/casos", new { flujoId, flujoVersionId = (Guid?)null, titulo = "Sigue activo", datosJson = (string?)null }));
        var inexistente = Guid.NewGuid();

        var respuesta = await client.PostAsJsonAsync("/api/v1/casos/acciones/reprocesar", new { ids = new[] { fallido1, fallido2, activo, inexistente } });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var resultado = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, resultado.GetProperty("procesados").GetInt32());
        var motivos = resultado.GetProperty("omitidos").EnumerateArray().ToDictionary(o => o.GetProperty("id").GetGuid(), o => o.GetProperty("motivo").GetString());
        Assert.Equal("Solo se reprocesan los casos fallidos.", motivos[activo]);
        Assert.Equal("Caso no encontrado.", motivos[inexistente]);

        // They are no longer failed: the step is waiting again (here, for its reviewer).
        Assert.NotEqual("Fallido", await EstadoAsync(client, fallido1));
        Assert.NotEqual("Fallido", await EstadoAsync(client, fallido2));
        Assert.Equal("EsperandoRevisionHumana", await EstadoAsync(client, fallido1));

        // Doing it again changes nothing: they are not failed any more.
        var otraVez = await (await client.PostAsJsonAsync("/api/v1/casos/acciones/reprocesar", new { ids = new[] { fallido1 } })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, otraVez.GetProperty("procesados").GetInt32());
    }

    [Fact]
    public async Task UnCasoCancelado_NoSeReprocesa()
    {
        var (client, userId) = await AdminAsync();
        var flujoId = await IdAsync(await client.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Proceso {Guid.NewGuid():N}", descripcion = (string?)null }));
        var versionId = await IdAsync(await client.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        await client.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new[] { new { orden = 1, nombre = "Revisar", tipoPaso = "RevisionHumana", agenteDefinicionId = (Guid?)null, configuracionJson = (string?)null } },
        });
        await client.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null);
        await client.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/asignaciones", new { userId });
        var caso = await IdAsync(await client.PostAsJsonAsync("/api/v1/casos", new { flujoId, flujoVersionId = (Guid?)null, titulo = "A cancelar", datosJson = (string?)null }));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/v1/casos/{caso}/cancelar", null)).StatusCode);

        var resultado = await (await client.PostAsJsonAsync("/api/v1/casos/acciones/reprocesar", new { ids = new[] { caso } })).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, resultado.GetProperty("procesados").GetInt32());
        Assert.Equal("Cancelado", await EstadoAsync(client, caso));
    }
}
