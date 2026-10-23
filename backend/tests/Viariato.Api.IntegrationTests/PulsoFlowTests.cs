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

/// <summary>
/// The panel and a case's screen poll a signature and only reload when it changes: it has to stay put while nothing happens and move
/// as soon as something they show does.
/// </summary>
public sealed class PulsoFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private async Task<(HttpClient Admin, Guid FlujoId)> CrearEntornoAsync()
    {
        var admin = factory.CreateClient();
        var email = $"pulso-{Guid.NewGuid():N}@example.com";
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

        return (admin, flujoId);
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<string> FirmaAsync(HttpClient client, string ruta)
    {
        var response = await client.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("firma").GetString()!;
    }

    [Fact]
    public async Task ACasesSignature_StaysWhileNothingHappens_AndMovesWhenItDoes()
    {
        var (admin, flujoId) = await CrearEntornoAsync();
        var casoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/casos", new { flujoId, titulo = "Vivo" }));

        var antes = await FirmaAsync(admin, $"/api/v1/casos/{casoId}/pulso");
        Assert.Equal(antes, await FirmaAsync(admin, $"/api/v1/casos/{casoId}/pulso"));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/casos/{casoId}/cancelar", null)).StatusCode);

        Assert.NotEqual(antes, await FirmaAsync(admin, $"/api/v1/casos/{casoId}/pulso"));
    }

    [Fact]
    public async Task ThePanelsSignature_MovesWhenACaseIsCreatedOrChanges()
    {
        var (admin, flujoId) = await CrearEntornoAsync();

        var vacio = await FirmaAsync(admin, "/api/v1/casos/resumen/pulso");
        var casoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/casos", new { flujoId, titulo = "Nuevo" }));
        var conUno = await FirmaAsync(admin, "/api/v1/casos/resumen/pulso");
        Assert.NotEqual(vacio, conUno);
        Assert.Equal(conUno, await FirmaAsync(admin, "/api/v1/casos/resumen/pulso"));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/v1/casos/{casoId}/cancelar", null)).StatusCode);

        Assert.NotEqual(conUno, await FirmaAsync(admin, "/api/v1/casos/resumen/pulso"));
    }

    [Fact]
    public async Task ACaseOfAProcessNotAssigned_IsNotFound()
    {
        var (admin, flujoId) = await CrearEntornoAsync();
        var casoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/casos", new { flujoId, titulo = "Ajeno" }));
        var (otro, _) = await CrearEntornoAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await otro.GetAsync($"/api/v1/casos/{casoId}/pulso")).StatusCode);
    }
}
