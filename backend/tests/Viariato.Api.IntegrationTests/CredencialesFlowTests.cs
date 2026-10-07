using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.RpaFleet.Domain;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Authorization;
using Viriato.Rpa.Client;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>
/// The credentials vault end to end against the real Api and Postgres: an admin stores a secret through the
/// admin API, a robot reads it back with the Viriato.Rpa.Client library using only its Despliegue's key — and
/// everything that must NOT work (reading it as an admin, as another robot, while switched off, tampered) doesn't.
/// </summary>
public sealed class CredencialesFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private const string Password = "S3cr3t-Pa$$w0rd!";

    private sealed record Entorno(HttpClient Admin, Guid ServicioA, Guid ServicioB, string KeyA, string KeyB, Guid DespliegueA);

    private async Task<Entorno> CrearEntornoAsync()
    {
        var admin = factory.CreateClient();
        var email = $"cred-{Guid.NewGuid():N}@example.com";

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
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var equipoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/equipos", new { nombre = $"Equipo {Guid.NewGuid():N}", descripcion = (string?)null }));
        var servicioA = await IdAsync(await admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Servicio A {Guid.NewGuid():N}", descripcion = (string?)null }));
        var servicioB = await IdAsync(await admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Servicio B {Guid.NewGuid():N}", descripcion = (string?)null }));
        var flujoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Flujo {Guid.NewGuid():N}", descripcion = (string?)null }));

        var (keyA, despliegueA) = await CrearDespliegueAsync(admin, equipoId, servicioA, flujoId);
        var (keyB, _) = await CrearDespliegueAsync(admin, equipoId, servicioB, flujoId);
        return new Entorno(admin, servicioA, servicioB, keyA, keyB, despliegueA);
    }

    private static async Task<(string Key, Guid DespliegueId)> CrearDespliegueAsync(HttpClient admin, Guid equipoId, Guid servicioId, Guid flujoId)
    {
        var respuesta = await admin.PostAsJsonAsync("/api/v1/despliegues", new { equipoId, servicioId, flujoId });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return (cuerpo.GetProperty("apiKey").GetString()!, cuerpo.GetProperty("despliegue").GetProperty("id").GetGuid());
    }

    private static async Task<Guid> IdAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static string NombreUnico() => $"portal-{Guid.NewGuid():N}";

    private static Task<HttpResponseMessage> Crear(HttpClient admin, string nombre, Guid? servicioId = null, string password = Password, string usuario = "robot") =>
        admin.PostAsJsonAsync("/api/v1/credenciales", new { nombre, descripcion = "Portal de pruebas", usuario, password, servicioId });

    private RpaClient Robot(string apiKey)
    {
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return new RpaClient(http);
    }

    [Fact]
    public async Task CreatingACredential_NeverReturnsThePassword_AndStoresItOnlyEncrypted()
    {
        var entorno = await CrearEntornoAsync();
        var nombre = NombreUnico();

        var creada = await Crear(entorno.Admin, nombre);
        var cuerpoCreada = await creada.Content.ReadAsStringAsync();
        var listado = await (await entorno.Admin.GetAsync($"/api/v1/credenciales?searchTerm={nombre}")).Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, creada.StatusCode);
        Assert.DoesNotContain(Password, cuerpoCreada);
        Assert.DoesNotContain(Password, listado);
        Assert.DoesNotContain("password", cuerpoCreada, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nombre, listado);

        using var scope = factory.Services.CreateScope();
        var guardada = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<Credencial>().SingleAsync(c => c.Nombre == nombre);
        Assert.NotEqual(Password, guardada.PasswordCifrado);
        Assert.DoesNotContain(Password, guardada.PasswordCifrado);
    }

    [Fact]
    public async Task InvalidRequests_AreRejected_WithTheRightStatus()
    {
        var entorno = await CrearEntornoAsync();
        var nombre = NombreUnico();
        Assert.Equal(HttpStatusCode.OK, (await Crear(entorno.Admin, nombre)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await Crear(entorno.Admin, "Mayusculas-No")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Crear(entorno.Admin, "con espacios")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Crear(entorno.Admin, NombreUnico(), password: "")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Crear(entorno.Admin, nombre)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Crear(entorno.Admin, NombreUnico(), servicioId: Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task ARobot_ReadsTheDecryptedCredential_AnyCasing_AndTheAccessIsRecorded()
    {
        var entorno = await CrearEntornoAsync();
        var nombre = NombreUnico();
        Assert.Equal(HttpStatusCode.OK, (await Crear(entorno.Admin, nombre)).StatusCode);

        var credencial = await Robot(entorno.KeyA).ObtenerCredencialAsync(nombre.ToUpperInvariant());

        Assert.Equal(nombre, credencial.Nombre);
        Assert.Equal("robot", credencial.Usuario);
        Assert.Equal(Password, credencial.Password);

        var listado = await entorno.Admin.GetFromJsonAsync<JsonElement>($"/api/v1/credenciales?searchTerm={nombre}");
        Assert.NotEqual(JsonValueKind.Null, listado.GetProperty("items")[0].GetProperty("ultimoAccesoAt").ValueKind);
    }

    [Fact]
    public async Task TheSecretIsNeverCached()
    {
        var entorno = await CrearEntornoAsync();
        var nombre = NombreUnico();
        await Crear(entorno.Admin, nombre);
        var http = factory.CreateClient();
        http.DefaultRequestHeaders.Add("X-Api-Key", entorno.KeyA);

        var respuesta = await http.GetAsync($"/api/v1/rpa/credenciales/{nombre}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("no-store", respuesta.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task UpdatingWithoutAPassword_KeepsTheStoredOne_AndANewOneReplacesIt()
    {
        var entorno = await CrearEntornoAsync();
        var nombre = NombreUnico();
        var id = await IdAsync(await Crear(entorno.Admin, nombre));
        var robot = Robot(entorno.KeyA);

        var sinCambiarPassword = await entorno.Admin.PutAsJsonAsync(
            $"/api/v1/credenciales/{id}", new { descripcion = "nueva", usuario = "otro-usuario", servicioId = (Guid?)null, activo = true, password = (string?)null });
        Assert.Equal(HttpStatusCode.OK, sinCambiarPassword.StatusCode);
        var tras = await robot.ObtenerCredencialAsync(nombre);
        Assert.Equal("otro-usuario", tras.Usuario);
        Assert.Equal(Password, tras.Password);

        var conPasswordNueva = await entorno.Admin.PutAsJsonAsync(
            $"/api/v1/credenciales/{id}", new { descripcion = "nueva", usuario = "otro-usuario", servicioId = (Guid?)null, activo = true, password = "otra-clave-9" });
        Assert.Equal(HttpStatusCode.OK, conPasswordNueva.StatusCode);
        Assert.Equal("otra-clave-9", (await robot.ObtenerCredencialAsync(nombre)).Password);
    }

    [Fact]
    public async Task ACredentialRestrictedToAServicio_IsOnlyServedToThatServiciosRobots()
    {
        var entorno = await CrearEntornoAsync();
        var restringida = NombreUnico();
        var paraTodos = NombreUnico();
        await Crear(entorno.Admin, restringida, servicioId: entorno.ServicioA);
        await Crear(entorno.Admin, paraTodos);

        Assert.Equal(Password, (await Robot(entorno.KeyA).ObtenerCredencialAsync(restringida)).Password);

        var ajeno = await Assert.ThrowsAsync<ViriatoApiException>(() => Robot(entorno.KeyB).ObtenerCredencialAsync(restringida));
        Assert.True(ajeno.IsNotFound);
        Assert.Equal(Password, (await Robot(entorno.KeyB).ObtenerCredencialAsync(paraTodos)).Password);
    }

    [Fact]
    public async Task AnInactiveCredential_IsNotServed_UntilItIsReactivated()
    {
        var entorno = await CrearEntornoAsync();
        var nombre = NombreUnico();
        var id = await IdAsync(await Crear(entorno.Admin, nombre));
        var robot = Robot(entorno.KeyA);

        await entorno.Admin.PutAsJsonAsync($"/api/v1/credenciales/{id}", new { descripcion = (string?)null, usuario = "robot", servicioId = (Guid?)null, activo = false, password = (string?)null });
        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => robot.ObtenerCredencialAsync(nombre));
        Assert.True(ex.IsNotFound);

        await entorno.Admin.PutAsJsonAsync($"/api/v1/credenciales/{id}", new { descripcion = (string?)null, usuario = "robot", servicioId = (Guid?)null, activo = true, password = (string?)null });
        Assert.Equal(Password, (await robot.ObtenerCredencialAsync(nombre)).Password);
    }

    [Fact]
    public async Task ASwitchedOffDespliegue_CannotReadCredentials()
    {
        var entorno = await CrearEntornoAsync();
        var nombre = NombreUnico();
        await Crear(entorno.Admin, nombre);
        var apagar = await entorno.Admin.PatchAsJsonAsync($"/api/v1/despliegues/{entorno.DespliegueA}", new { encendido = false });
        Assert.Equal(HttpStatusCode.OK, apagar.StatusCode);

        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => Robot(entorno.KeyA).ObtenerCredencialAsync(nombre));

        Assert.True(ex.IsConflict);
    }

    [Fact]
    public async Task AMissingCredential_IsNotFound()
    {
        var entorno = await CrearEntornoAsync();

        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => Robot(entorno.KeyA).ObtenerCredencialAsync("no-existe"));

        Assert.True(ex.IsNotFound);
    }

    [Fact]
    public async Task OnlyARobotKeyOpensTheRobotEndpoint_AndOnlyAnAdminOpensTheAdminOne()
    {
        var entorno = await CrearEntornoAsync();
        var nombre = NombreUnico();
        await Crear(entorno.Admin, nombre);

        var clavesMala = await Assert.ThrowsAsync<ViriatoApiException>(() => Robot("rpa_clave-que-no-existe").ObtenerCredencialAsync(nombre));
        Assert.True(clavesMala.IsUnauthorized);

        // an admin's JWT is not a robot identity
        Assert.Equal(HttpStatusCode.Unauthorized, (await entorno.Admin.GetAsync($"/api/v1/rpa/credenciales/{nombre}")).StatusCode);

        // and a robot's key cannot manage credentials
        var robot = factory.CreateClient();
        robot.DefaultRequestHeaders.Add("X-Api-Key", entorno.KeyA);
        Assert.Equal(HttpStatusCode.Unauthorized, (await robot.GetAsync("/api/v1/credenciales")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/credenciales")).StatusCode);
    }

    [Fact]
    public async Task AServicioWithCredentials_CannotBeDeleted_UntilTheyAreGone()
    {
        var entorno = await CrearEntornoAsync();
        var servicioPropio = await IdAsync(await entorno.Admin.PostAsJsonAsync("/api/v1/servicios", new { nombre = $"Solo credenciales {Guid.NewGuid():N}", descripcion = (string?)null }));
        var credencialId = await IdAsync(await Crear(entorno.Admin, NombreUnico(), servicioId: servicioPropio));

        var refused = await entorno.Admin.DeleteAsync($"/api/v1/servicios/{servicioPropio}");
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await entorno.Admin.DeleteAsync($"/api/v1/credenciales/{credencialId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await entorno.Admin.DeleteAsync($"/api/v1/servicios/{servicioPropio}")).StatusCode);
    }

    [Fact]
    public async Task ATamperedRow_FailsWithoutLeakingAnything()
    {
        var entorno = await CrearEntornoAsync();
        var nombre = NombreUnico();
        await Crear(entorno.Admin, nombre);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fila = await db.Set<Credencial>().SingleAsync(c => c.Nombre == nombre);
            fila.PasswordCifrado = Convert.ToBase64String(new byte[64]);
            await db.SaveChangesAsync();
        }

        var ex = await Assert.ThrowsAsync<ViriatoApiException>(() => Robot(entorno.KeyA).ObtenerCredencialAsync(nombre));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.DoesNotContain(Password, ex.Message);
    }
}
