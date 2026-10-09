using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Viariato.Infrastructure;
using Viariato.Modules.Users.Domain;
using Viariato.Shared.Authorization;
using Xunit;

namespace Viariato.Api.IntegrationTests;

/// <summary>The bulk action "download documents": one zip with a folder per case, only what the Documentos tab lists.</summary>
public sealed class DescargaMasivaDeDocumentosFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private sealed record Entorno(HttpClient Admin, Guid FlujoId, Guid UserId);

    private async Task<(HttpClient Client, Guid UserId)> UsuarioAsync(string? rol, params string[] permisosSueltos)
    {
        var client = factory.CreateClient();
        var email = $"zip-{Guid.NewGuid():N}@example.com";

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password = "SuperSecret123", displayName = "Prueba" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var userId = (await register.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("user").GetProperty("id").GetGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
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
                    foreach (var permiso in await db.Set<Permission>().Where(p => permisosSueltos.Contains(p.Name)).ToListAsync())
                    {
                        db.Add(new RolePermission { RoleId = role.Id, PermissionId = permiso.Id });
                    }
                }

                db.Add(new UserRole { UserId = userId, RoleId = role.Id, GrantedAt = DateTimeOffset.UtcNow });
                await db.SaveChangesAsync();
            }
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

    private async Task<Entorno> EntornoAsync()
    {
        var (admin, userId) = await UsuarioAsync(SystemRoles.Admin);
        var flujoId = await IdAsync(await admin.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Proceso {Guid.NewGuid():N}", descripcion = (string?)null }));
        var versionId = await IdAsync(await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        await admin.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new[] { new { orden = 1, nombre = "Paso", tipoPaso = "Interno", agenteDefinicionId = (Guid?)null, configuracionJson = (string?)null } },
        });
        await admin.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/asignaciones", new { userId })).StatusCode);
        return new Entorno(admin, flujoId, userId);
    }

    private static async Task<Guid> CasoAsync(HttpClient client, Guid flujoId, string titulo) =>
        await IdAsync(await client.PostAsJsonAsync("/api/v1/casos", new { flujoId, flujoVersionId = (Guid?)null, titulo, datosJson = (string?)null }));

    private static async Task SubirAsync(HttpClient client, Guid casoId, string nombre, string contenido)
    {
        using var form = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes(contenido)), "file", nombre } };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/casos/{casoId}/documentos", form)).StatusCode);
    }

    private static Task<HttpResponseMessage> ZipAsync(HttpClient client, params Guid[] ids) =>
        client.PostAsJsonAsync("/api/v1/casos/documentos/zip", new { ids });

    private static JsonElement Resumen(HttpResponseMessage respuesta)
    {
        var cabecera = Assert.Single(respuesta.Headers.GetValues("X-Viriato-Resumen"));
        return JsonDocument.Parse(Convert.FromBase64String(cabecera)).RootElement;
    }

    private static async Task<Dictionary<string, string>> EntradasAsync(HttpResponseMessage respuesta)
    {
        await using var flujo = await respuesta.Content.ReadAsStreamAsync();
        using var zip = new ZipArchive(flujo, ZipArchiveMode.Read);
        var entradas = new Dictionary<string, string>();
        foreach (var entrada in zip.Entries)
        {
            using var lector = new StreamReader(entrada.Open(), Encoding.UTF8);
            entradas[entrada.FullName] = await lector.ReadToEndAsync();
        }

        return entradas;
    }

    [Fact]
    public async Task UnZip_LlevaUnaCarpetaPorCaso_ConSusDocumentos_YUnLeeme()
    {
        var e = await EntornoAsync();
        var a = await CasoAsync(e.Admin, e.FlujoId, "Balay lavadoras");
        var b = await CasoAsync(e.Admin, e.FlujoId, "Bosch frigoríficos");
        var vacio = await CasoAsync(e.Admin, e.FlujoId, "Sin nada");
        await SubirAsync(e.Admin, a, "precios.csv", "SKU,Precio\n1,2");
        await SubirAsync(e.Admin, a, "notas.txt", "hola");
        await SubirAsync(e.Admin, b, "precios.csv", "SKU,Precio\n9,9");

        var respuesta = await ZipAsync(e.Admin, a, b, vacio);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/zip", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("documentos-", respuesta.Content.Headers.ContentDisposition?.FileNameStar ?? respuesta.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        var entradas = await EntradasAsync(respuesta);
        Assert.Equal("SKU,Precio\n1,2", entradas[$"Balay lavadoras ({a.ToString("N")[..8]})/precios.csv"]);
        Assert.Equal("hola", entradas[$"Balay lavadoras ({a.ToString("N")[..8]})/notas.txt"]);
        Assert.Equal("SKU,Precio\n9,9", entradas[$"Bosch frigoríficos ({b.ToString("N")[..8]})/precios.csv"]);
        Assert.DoesNotContain(entradas.Keys, k => k.Contains("Sin nada"));
        Assert.Contains("3 documentos de 2 casos", entradas["LEEME.txt"]);
        Assert.Contains("No tiene documentos.", entradas["LEEME.txt"]);
    }

    [Fact]
    public async Task ElResumen_DiceCuantosDocumentosYQueCasosSeQuedaronFuera()
    {
        var e = await EntornoAsync();
        var con = await CasoAsync(e.Admin, e.FlujoId, "Con documento");
        var sin = await CasoAsync(e.Admin, e.FlujoId, "Sin documento");
        await SubirAsync(e.Admin, con, "a.txt", "a");
        var inexistente = Guid.NewGuid();

        var respuesta = await ZipAsync(e.Admin, con, sin, inexistente);

        var resumen = Resumen(respuesta);
        Assert.Equal(1, resumen.GetProperty("documentos").GetInt32());
        Assert.Equal(1, resumen.GetProperty("casosConDocumentos").GetInt32());
        Assert.Equal(2, resumen.GetProperty("omitidosTotal").GetInt32());
        var motivos = resumen.GetProperty("omitidos").EnumerateArray().ToDictionary(o => o.GetProperty("id").GetGuid(), o => o.GetProperty("motivo").GetString());
        Assert.Equal("No tiene documentos.", motivos[sin]);
        Assert.Equal("Caso no encontrado.", motivos[inexistente]);
    }

    [Fact]
    public async Task DosDocumentosConElMismoNombre_NoSePisan()
    {
        var e = await EntornoAsync();
        var caso = await CasoAsync(e.Admin, e.FlujoId, "Repetidos");
        await SubirAsync(e.Admin, caso, "dni.txt", "uno");
        await SubirAsync(e.Admin, caso, "dni.txt", "dos");

        var entradas = await EntradasAsync(await ZipAsync(e.Admin, caso));

        var carpeta = $"Repetidos ({caso.ToString("N")[..8]})";
        Assert.Equal(["dos", "uno"], new[] { entradas[$"{carpeta}/dni.txt"], entradas[$"{carpeta}/dni (2).txt"] }.Order().ToArray());
    }

    [Fact]
    public async Task UnNombreQueIntentaSubirDeCarpeta_QuedaDentroDeLaSuya()
    {
        var e = await EntornoAsync();
        var caso = await CasoAsync(e.Admin, e.FlujoId, "../../Fuera");
        await SubirAsync(e.Admin, caso, "..\\..\\malo.txt", "x");

        var entradas = await EntradasAsync(await ZipAsync(e.Admin, caso));

        Assert.All(entradas.Keys, nombre =>
        {
            Assert.DoesNotContain("..", nombre.Split('/'));
            Assert.False(nombre.StartsWith('/'));
            Assert.DoesNotContain('\\', nombre);
        });
        Assert.Equal(1, entradas.Keys.Count(k => k != "LEEME.txt" && k.Split('/').Length == 2));
    }

    [Fact]
    public async Task SinDocumentosEnNingunCaso_NoHayZip_PeroSiResumen()
    {
        var e = await EntornoAsync();
        var caso = await CasoAsync(e.Admin, e.FlujoId, "Vacío");

        var respuesta = await ZipAsync(e.Admin, caso);

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
        Assert.Equal(1, Resumen(respuesta).GetProperty("omitidosTotal").GetInt32());
    }

    [Fact]
    public async Task UnCasoDeUnProcesoSinAsignar_SeTrataComoSiNoExistiera()
    {
        var e = await EntornoAsync();
        var propio = await CasoAsync(e.Admin, e.FlujoId, "Mío");
        await SubirAsync(e.Admin, propio, "a.txt", "a");

        var otro = await EntornoAsync();
        var ajeno = await CasoAsync(otro.Admin, otro.FlujoId, "Ajeno");
        await SubirAsync(otro.Admin, ajeno, "secreto.txt", "no debe salir");

        var respuesta = await ZipAsync(e.Admin, propio, ajeno);

        var entradas = await EntradasAsync(respuesta);
        Assert.DoesNotContain(entradas, kv => kv.Value.Contains("no debe salir"));
        Assert.DoesNotContain(entradas.Keys, k => k.Contains("Ajeno"));
        var resumen = Resumen(respuesta);
        Assert.Equal(1, resumen.GetProperty("documentos").GetInt32());
        Assert.Equal("Caso no encontrado.", resumen.GetProperty("omitidos")[0].GetProperty("motivo").GetString());
    }

    [Fact]
    public async Task LaSeleccion_NoPuedeEstarVacia_NiPasarDelMaximo()
    {
        var e = await EntornoAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await ZipAsync(e.Admin)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ZipAsync(e.Admin, Enumerable.Range(0, 501).Select(_ => Guid.NewGuid()).ToArray())).StatusCode);
    }

    [Fact]
    public async Task HaceFaltaElPermisoDeDescargar()
    {
        var e = await EntornoAsync();
        var (sinPermiso, _) = await UsuarioAsync(null, Permissions.CasosRead, Permissions.CasosMasivas);
        var (conPermiso, _) = await UsuarioAsync(null, Permissions.CasosDescargar);

        Assert.Equal(HttpStatusCode.Forbidden, (await ZipAsync(sinPermiso, Guid.NewGuid())).StatusCode);
        // With it the door opens: nothing to give (the case is not theirs), so it is the report, not a refusal.
        Assert.Equal(HttpStatusCode.NoContent, (await ZipAsync(conPermiso, Guid.NewGuid())).StatusCode);
        Assert.Contains(Permissions.CasosDescargar, Permissions.ParaElRolUser);
    }
}
