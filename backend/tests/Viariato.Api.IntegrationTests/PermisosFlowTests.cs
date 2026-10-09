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
/// Who can do what: the Admin role has every permission, the User role (what people get on registering) can use the platform and
/// cannot configure it, and the finer permissions — cancel, reprioritise, bulk actions, dispatch, credentials, creators — are
/// each really required by what they guard.
/// </summary>
public sealed class PermisosFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private async Task<HttpClient> UsuarioAsync(string? rol, params string[] permisosSueltos)
    {
        var client = factory.CreateClient();
        var email = $"permisos-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "SuperSecret123", displayName = "Prueba" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("id").GetGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Registering already gave the User role; "rol" null with loose permissions builds a role of exactly those.
            if (rol is not null || permisosSueltos.Length > 0)
            {
                db.RemoveRange(await db.Set<UserRole>().Where(ur => ur.UserId == userId).ToListAsync());
                Role role;
                if (rol is not null)
                {
                    role = await db.Set<Role>().SingleAsync(r => r.Name == rol);
                }
                else
                {
                    role = new Role { Name = $"Rol de prueba {Guid.NewGuid():N}", CreatedAt = DateTimeOffset.UtcNow };
                    db.Add(role);
                    var permisos = await db.Set<Permission>().Where(p => permisosSueltos.Contains(p.Name)).ToListAsync();
                    Assert.Equal(permisosSueltos.Length, permisos.Count);
                    foreach (var permiso in permisos) db.Add(new RolePermission { RoleId = role.Id, PermissionId = permiso.Id });
                }

                db.Add(new UserRole { UserId = userId, RoleId = role.Id, GrantedAt = DateTimeOffset.UtcNow });
                await db.SaveChangesAsync();
            }
        }

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "SuperSecret123" });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HashSet<string>> PermisosDelRolAsync(string nombre)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Set<RolePermission>()
            .Where(rp => rp.Role.Name == nombre)
            .Select(rp => rp.Permission.Name)
            .ToListAsync()).ToHashSet();
    }

    [Fact]
    public async Task ElAdministrador_TieneTodosLosPermisos()
    {
        var permisos = await PermisosDelRolAsync(SystemRoles.Admin);

        Assert.Equal(Permissions.All.ToHashSet(), permisos);
    }

    [Fact]
    public async Task ElRolUser_TieneLoDeUsar_YNadaDeConfigurar()
    {
        var permisos = await PermisosDelRolAsync(SystemRoles.User);

        Assert.True(Permissions.ParaElRolUser.ToHashSet().IsSubsetOf(permisos), "falta alguno de los permisos de uso");
        Assert.DoesNotContain(Permissions.FlujosManage, permisos);
        Assert.DoesNotContain(Permissions.RpaManage, permisos);
        Assert.DoesNotContain(Permissions.RpaDespacho, permisos);
        Assert.DoesNotContain(Permissions.RpaCredenciales, permisos);
        Assert.DoesNotContain(Permissions.FlujosCreadores, permisos);
        Assert.DoesNotContain(Permissions.UsersManage, permisos);
        Assert.DoesNotContain(Permissions.RolesManage, permisos);
    }

    [Fact]
    public async Task UnUsuarioNormal_PuedeUsarLaPlataforma()
    {
        var usuario = await UsuarioAsync(null);

        Assert.Equal(HttpStatusCode.OK, (await usuario.GetAsync("/api/v1/casos/resumen")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await usuario.GetAsync("/api/v1/casos?page=1&pageSize=5")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await usuario.GetAsync("/api/v1/creadores-de-caso")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await usuario.GetAsync("/api/v1/flujos/asignados")).StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/v1/credenciales")]
    [InlineData("GET", "/api/v1/plantillas-despacho")]
    [InlineData("GET", "/api/v1/equipos")]
    [InlineData("GET", "/api/v1/servicios")]
    [InlineData("GET", "/api/v1/despliegues")]
    [InlineData("GET", "/api/v1/users")]
    [InlineData("GET", "/api/v1/roles")]
    [InlineData("POST", "/api/v1/flujos")]
    [InlineData("POST", "/api/v1/casos")]
    public async Task UnUsuarioNormal_NoPuedeConfigurarLaPlataforma(string metodo, string ruta)
    {
        var usuario = await UsuarioAsync(null);

        var respuesta = await usuario.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), ruta)
        {
            Content = metodo == "POST" ? JsonContent.Create(new { nombre = "x" }) : null,
        });

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task CancelarYPriorizar_PidenSuPropioPermiso_NoBastaConOperarElCaso()
    {
        // Can operate cases (casos.manage) and see them, but was not given cancel, priority or the bulk screen.
        var operador = await UsuarioAsync(null, Permissions.CasosRead, Permissions.CasosManage);
        var caso = Guid.NewGuid();
        var paso = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await operador.PostAsync($"/api/v1/casos/{caso}/cancelar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operador.PostAsync($"/api/v1/casos/{caso}/pasos/{paso}/cancelar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operador.PatchAsJsonAsync($"/api/v1/casos/{caso}/pasos/{paso}/prioridad", new { prioridad = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await operador.PostAsJsonAsync("/api/v1/casos/acciones/cancelar", new { ids = new[] { caso } })).StatusCode);
        // What they were given does open the door (the case does not exist, so it is a 404, not a 403).
        Assert.Equal(HttpStatusCode.NotFound, (await operador.PostAsync($"/api/v1/casos/{caso}/pausar", null)).StatusCode);
    }

    [Fact]
    public async Task LaPantallaDeAccionesMasivas_NoBasta_ParaLanzarUnaAccionQueRequiereOtroPermiso()
    {
        var soloLaPantalla = await UsuarioAsync(null, Permissions.CasosRead, Permissions.CasosMasivas);

        var respuesta = await soloLaPantalla.PostAsJsonAsync("/api/v1/casos/acciones/cancelar", new { ids = new[] { Guid.NewGuid() } });

        // Through the screen's own door, but "cancelar" asks for casos.cancelar too.
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task ElDespachoYLasCredenciales_SonPermisosApartes_DeGestionarLaFlota()
    {
        var soloFlota = await UsuarioAsync(null, Permissions.RpaManage);
        var soloDespacho = await UsuarioAsync(null, Permissions.RpaDespacho);
        var soloCredenciales = await UsuarioAsync(null, Permissions.RpaCredenciales);

        Assert.Equal(HttpStatusCode.OK, (await soloFlota.GetAsync("/api/v1/equipos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await soloFlota.GetAsync("/api/v1/plantillas-despacho")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await soloFlota.GetAsync("/api/v1/credenciales")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await soloDespacho.GetAsync("/api/v1/plantillas-despacho")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await soloDespacho.GetAsync("/api/v1/credenciales")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await soloDespacho.GetAsync("/api/v1/equipos")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await soloCredenciales.GetAsync("/api/v1/credenciales")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await soloCredenciales.GetAsync("/api/v1/plantillas-despacho")).StatusCode);
    }

    [Fact]
    public async Task ConfigurarLosCreadores_PideSuPermiso_PeroVerlosSoloPideLeerElProceso()
    {
        var admin = await UsuarioAsync(SystemRoles.Admin);
        var flujo = (await (await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Proceso {Guid.NewGuid():N}", descripcion = (string?)null })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var lector = await UsuarioAsync(null, Permissions.FlujosRead);
        var gestor = await UsuarioAsync(null, Permissions.FlujosRead, Permissions.FlujosManage);
        var cuerpo = new { nombre = "Alta", descripcion = (string?)null, tipoCasoId = (Guid?)null, pasoInicialNombre = (string?)null, estadoNegocioInicialId = (Guid?)null, plantillaTitulo = (string?)null, orden = 1, activo = true };

        Assert.Equal(HttpStatusCode.OK, (await lector.GetAsync($"/api/v1/flujos/{flujo}/creadores")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await lector.PostAsJsonAsync($"/api/v1/flujos/{flujo}/creadores", cuerpo)).StatusCode);
        // Managing the process is not enough either: creators have their own permission.
        Assert.Equal(HttpStatusCode.Forbidden, (await gestor.PostAsJsonAsync($"/api/v1/flujos/{flujo}/creadores", cuerpo)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/flujos/{flujo}/creadores", cuerpo)).StatusCode);
    }
}
