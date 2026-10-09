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
/// End-to-end coverage of the Flujos/Casos orchestration engine against a real Postgres — this is
/// what caught the ReplacePasosAsync bug where EF's key-is-set heuristic turned INSERTs into no-op
/// UPDATEs when new FlujoPasoDef rows were added via a tracked navigation collection instead of
/// db.Add. Unit tests (InMemory provider) didn't reproduce that; only a real database does.
/// </summary>
public sealed class FlujosAndCasosFlowTests(ViariatoApiFactory factory) : IClassFixture<ViariatoApiFactory>
{
    private static string RandomEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private sealed record AuthorizedClient(HttpClient Client, Guid UserId);

    private async Task<AuthorizedClient> CreateAuthorizedClientAsync()
    {
        var client = factory.CreateClient();
        var email = RandomEmail();

        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "SuperSecret123",
            displayName = "Test User",
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var registered = await register.Content.ReadFromJsonAsync<JsonElement>();
        var userId = registered.GetProperty("user").GetProperty("id").GetGuid();

        await GrantAdminRoleAsync(userId);

        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "SuperSecret123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = loginBody.GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return new AuthorizedClient(client, userId);
    }

    /// <summary>Every Casos endpoint now also enforces AsignacionFlujo — having FlujosManage lets this
    /// same client self-assign right after creating the Flujo, same as an admin would in practice.</summary>
    private static async Task AsignarFlujoAsync(HttpClient client, Guid flujoId, Guid userId)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/asignaciones", new { userId });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Grants the Admin role directly via the DB (same shape as the Bootstrap admin) since
    /// the test factory doesn't configure Bootstrap:AdminEmail/Password — a freshly registered user
    /// otherwise has no Flujos/Casos permissions to exercise these endpoints.</summary>
    private async Task GrantAdminRoleAsync(Guid userId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var adminRole = await db.Set<Role>().SingleAsync(r => r.Name == SystemRoles.Admin);
        db.Add(new UserRole { UserId = userId, RoleId = adminRole.Id, GrantedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> ExtractIdAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

    private static async Task<string?> ExtractStringAsync(HttpResponseMessage response, string field) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty(field).GetString();

    [Fact]
    public async Task CrearVersionarYPublicarFlujo_LeavesTheVersionPublishedWithItsSteps()
    {
        var (client, _) = await CreateAuthorizedClientAsync();

        var flujoResponse = await client.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Flujo {Guid.NewGuid():N}", descripcion = (string?)null });
        Assert.Equal(HttpStatusCode.OK, flujoResponse.StatusCode);
        var flujoId = await ExtractIdAsync(flujoResponse);

        var versionResponse = await client.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null });
        Assert.Equal(HttpStatusCode.OK, versionResponse.StatusCode);
        var versionId = await ExtractIdAsync(versionResponse);

        // This is exactly the request that used to 500: replacing an empty step list with two new
        // ones on a version that was just loaded (and is therefore already tracked).
        var pasosResponse = await client.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new[]
            {
                new { orden = 1, nombre = "Paso A", tipoPaso = "Interno", agenteDefinicionId = (Guid?)null, configuracionJson = (string?)null },
                new { orden = 2, nombre = "Paso B", tipoPaso = "Interno", agenteDefinicionId = (Guid?)null, configuracionJson = (string?)null },
            },
        });
        Assert.Equal(HttpStatusCode.OK, pasosResponse.StatusCode);

        var publishResponse = await client.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);
        Assert.Equal("Publicada", await ExtractStringAsync(publishResponse, "estado"));

        var flujoDetail = await client.GetFromJsonAsync<JsonElement>($"/api/v1/flujos/{flujoId}");
        Assert.Equal(versionId, flujoDetail.GetProperty("versionActivaId").GetGuid());
    }

    [Fact]
    public async Task IniciarCaso_LinearFlow_CompletesEndToEndThroughTheRealDatabase()
    {
        var (client, userId) = await CreateAuthorizedClientAsync();

        var flujoId = await ExtractIdAsync(await client.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Flujo {Guid.NewGuid():N}", descripcion = (string?)null }));
        await AsignarFlujoAsync(client, flujoId, userId);
        var versionId = await ExtractIdAsync(await client.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        await client.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new[] { new { orden = 1, nombre = "Unico paso", tipoPaso = "Interno", agenteDefinicionId = (Guid?)null, configuracionJson = (string?)null } },
        });
        await client.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null);

        var casoResponse = await client.PostAsJsonAsync("/api/v1/casos", new { flujoId, flujoVersionId = (Guid?)null, titulo = "Caso E2E", datosJson = (string?)null });
        Assert.Equal(HttpStatusCode.OK, casoResponse.StatusCode);
        Assert.Equal("Completado", await ExtractStringAsync(casoResponse, "estado"));
    }

    [Fact]
    public async Task RevisionHumana_RechazoLuegoReintentoYAprobacion_CompletesTheCaso()
    {
        var (client, userId) = await CreateAuthorizedClientAsync();

        var flujoId = await ExtractIdAsync(await client.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Flujo {Guid.NewGuid():N}", descripcion = (string?)null }));
        await AsignarFlujoAsync(client, flujoId, userId);
        var versionId = await ExtractIdAsync(await client.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        await client.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new[] { new { orden = 1, nombre = "Revisar", tipoPaso = "RevisionHumana", agenteDefinicionId = (Guid?)null, configuracionJson = (string?)null } },
        });
        await client.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null);

        var casoId = await ExtractIdAsync(await client.PostAsJsonAsync("/api/v1/casos", new { flujoId, flujoVersionId = (Guid?)null, titulo = "Caso Revision", datosJson = (string?)null }));

        var pasoId = await FindPendingRevisionPasoIdAsync(client, casoId);

        var rechazo = await client.PostAsJsonAsync($"/api/v1/revisiones/{pasoId}/resolver", new { decision = "Rechazada", comentario = "faltan datos" });
        Assert.Equal(HttpStatusCode.NoContent, rechazo.StatusCode);

        var trasRechazo = await client.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}");
        Assert.Equal("Fallido", trasRechazo.GetProperty("estado").GetString());

        var reintento = await client.PostAsync($"/api/v1/casos/{casoId}/pasos/{pasoId}/reprocesar", null);
        Assert.Equal(HttpStatusCode.NoContent, reintento.StatusCode);

        var nuevoPasoId = await FindPendingRevisionPasoIdAsync(client, casoId);

        var aprobacion = await client.PostAsJsonAsync($"/api/v1/revisiones/{nuevoPasoId}/resolver", new { decision = "Aprobada", comentario = (string?)null });
        Assert.Equal(HttpStatusCode.NoContent, aprobacion.StatusCode);

        var final = await client.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}");
        Assert.Equal("Completado", final.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task UnUsuarioSinAsignacionAlFlujo_NoPuedeVerNiListarElCasoDeOtroUsuario()
    {
        var (owner, ownerId) = await CreateAuthorizedClientAsync();
        var flujoId = await ExtractIdAsync(await owner.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Flujo {Guid.NewGuid():N}", descripcion = (string?)null }));
        await AsignarFlujoAsync(owner, flujoId, ownerId);
        var versionId = await ExtractIdAsync(await owner.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        await owner.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new[] { new { orden = 1, nombre = "Unico paso", tipoPaso = "Interno", agenteDefinicionId = (Guid?)null, configuracionJson = (string?)null } },
        });
        await owner.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null);
        var casoId = await ExtractIdAsync(await owner.PostAsJsonAsync("/api/v1/casos", new { flujoId, flujoVersionId = (Guid?)null, titulo = "Caso privado", datosJson = (string?)null }));

        // A second Admin-permissioned user, but with no AsignacionFlujo row for this Flujo.
        var (outsider, _) = await CreateAuthorizedClientAsync();

        var getResponse = await outsider.GetAsync($"/api/v1/casos/{casoId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);

        var listResponse = await outsider.GetFromJsonAsync<JsonElement>("/api/v1/casos");
        Assert.Equal(0, listResponse.GetProperty("total").GetInt32());

        var pauseResponse = await outsider.PostAsync($"/api/v1/casos/{casoId}/pausar", null);
        Assert.Equal(HttpStatusCode.NotFound, pauseResponse.StatusCode);

        // The owner, who is assigned, can still see it fine.
        var ownerGet = await owner.GetAsync($"/api/v1/casos/{casoId}");
        Assert.Equal(HttpStatusCode.OK, ownerGet.StatusCode);
    }

    [Fact]
    public async Task UnaEvidenciaConArchivo_NoApareceComoDocumentoNiDuplicadaEnLaLineaDeTiempo()
    {
        var (client, userId) = await CreateAuthorizedClientAsync();
        var flujoId = await ExtractIdAsync(await client.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Flujo {Guid.NewGuid():N}", descripcion = (string?)null }));
        await AsignarFlujoAsync(client, flujoId, userId);
        var versionId = await ExtractIdAsync(await client.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        await client.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new[] { new { orden = 1, nombre = "Unico paso", tipoPaso = "Interno", agenteDefinicionId = (Guid?)null, configuracionJson = (string?)null } },
        });
        await client.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null);
        var casoResponse = await client.PostAsJsonAsync("/api/v1/casos", new { flujoId, flujoVersionId = (Guid?)null, titulo = "Caso con archivos", datosJson = (string?)null });
        var casoId = await ExtractIdAsync(casoResponse);

        var ejecuciones = await client.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}/ejecuciones");
        var ejecucionId = ejecuciones[0].GetProperty("id").GetGuid();
        var ejecucion = await client.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}/ejecuciones/{ejecucionId}");
        var pasoId = ejecucion.GetProperty("pasos")[0].GetProperty("id").GetGuid();

        using var documentoForm = new MultipartFormDataContent { { new ByteArrayContent("dni"u8.ToArray()), "file", "dni.txt" } };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/casos/{casoId}/documentos", documentoForm)).StatusCode);

        using var evidenciaForm = new MultipartFormDataContent
        {
            { new StringContent("Screenshot"), "tipo" },
            { new StringContent("Captura del portal"), "titulo" },
            { new ByteArrayContent("png"u8.ToArray()), "file", "captura.png" },
        };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/casos/{casoId}/pasos/{pasoId}/evidencias", evidenciaForm)).StatusCode);

        var documentos = await client.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}/documentos");
        Assert.Equal(1, documentos.GetArrayLength());
        Assert.Equal("dni.txt", documentos[0].GetProperty("nombre").GetString());

        var timeline = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}/timeline")).EnumerateArray().ToList();
        Assert.Single(timeline, item => item.GetProperty("tipo").GetString() == "Documento");
        var evidencia = Assert.Single(timeline, item => item.GetProperty("tipo").GetString() == "Evidencia");

        // The detail the timeline expands to show: which step produced it and what the file is.
        Assert.Equal("Unico paso", evidencia.GetProperty("pasoNombre").GetString());
        Assert.Equal("captura.png", evidencia.GetProperty("nombreArchivo").GetString());
        Assert.Equal(3, evidencia.GetProperty("tamanoBytes").GetInt64());
    }

    [Fact]
    public async Task UnArchivoGeneradoPorUnPaso_EsUnDocumentoDelCaso_YLaLineaDeTiempoSoloLoResume()
    {
        var (client, userId) = await CreateAuthorizedClientAsync();
        var flujoId = await ExtractIdAsync(await client.PostAsJsonAsync("/api/v1/flujos", new { nombre = $"Flujo {Guid.NewGuid():N}", descripcion = (string?)null }));
        await AsignarFlujoAsync(client, flujoId, userId);
        var versionId = await ExtractIdAsync(await client.PostAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones", new { notas = (string?)null }));
        await client.PutAsJsonAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/pasos", new
        {
            pasos = new[] { new { orden = 1, nombre = "Genera informe", tipoPaso = "Interno", agenteDefinicionId = (Guid?)null, configuracionJson = (string?)null } },
        });
        await client.PostAsync($"/api/v1/flujos/{flujoId}/versiones/{versionId}/publicar", null);
        var casoId = await ExtractIdAsync(await client.PostAsJsonAsync("/api/v1/casos", new { flujoId, flujoVersionId = (Guid?)null, titulo = "Caso con informe", datosJson = (string?)null }));

        var ejecuciones = await client.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}/ejecuciones");
        var ejecucion = await client.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}/ejecuciones/{ejecuciones[0].GetProperty("id").GetGuid()}");
        var pasoId = ejecucion.GetProperty("pasos")[0].GetProperty("id").GetGuid();

        async Task SubirEvidenciaAsync(string tipo, string titulo, string archivo)
        {
            using var form = new MultipartFormDataContent
            {
                { new StringContent(tipo), "tipo" },
                { new StringContent(titulo), "titulo" },
                { new ByteArrayContent("x"u8.ToArray()), "file", archivo },
            };
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/casos/{casoId}/pasos/{pasoId}/evidencias", form)).StatusCode);
        }

        await SubirEvidenciaAsync("ArchivoGenerado", "Informe", "informe.csv");
        await SubirEvidenciaAsync("Screenshot", "Captura", "captura.png");

        // The generated file is a document (with the step that made it); the screenshot stays only in the timeline.
        var documentos = await client.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}/documentos");
        var documento = Assert.Single(documentos.EnumerateArray());
        Assert.Equal("informe.csv", documento.GetProperty("nombre").GetString());
        Assert.Equal("Genera informe", documento.GetProperty("pasoNombre").GetString());

        // The timeline still has both as evidence — and does not list the generated file a second time as a Documento.
        var timeline = (await client.GetFromJsonAsync<JsonElement>($"/api/v1/casos/{casoId}/timeline")).EnumerateArray().ToList();
        Assert.Equal(2, timeline.Count(item => item.GetProperty("tipo").GetString() == "Evidencia"));
        Assert.DoesNotContain(timeline, item => item.GetProperty("tipo").GetString() == "Documento");
    }

    private static async Task<Guid> FindPendingRevisionPasoIdAsync(HttpClient client, Guid casoId)
    {
        var pendientes = await client.GetFromJsonAsync<JsonElement>("/api/v1/revisiones?estado=pendiente");
        foreach (var pendiente in pendientes.EnumerateArray())
        {
            if (pendiente.GetProperty("casoId").GetGuid() == casoId)
            {
                return pendiente.GetProperty("ejecucionPasoId").GetGuid();
            }
        }

        throw new InvalidOperationException($"No pending review found for caso {casoId}.");
    }
}
