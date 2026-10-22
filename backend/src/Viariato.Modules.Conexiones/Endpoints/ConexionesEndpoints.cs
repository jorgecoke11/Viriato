using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Conexiones.Domain;
using Viariato.Modules.Conexiones.Tipos;
using Viariato.Modules.RpaFleet.Security;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.Conexiones.Endpoints;

public sealed record CampoDeConexionDto(string Codigo, string Etiqueta, string Tipo, bool Obligatorio, object? PorDefecto, string? Ayuda);

public sealed record TipoDeConexionDto(string Codigo, string Nombre, string? EtiquetaSecreto, bool EnviaMensajes, IReadOnlyList<CampoDeConexionDto> Campos);

public sealed record ConexionDto(
    Guid Id, string Nombre, string Tipo, string TipoNombre, JsonElement Configuracion, bool TieneSecreto, bool EnviaMensajes, bool Activa,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <param name="Secreto">Only when it changes: null or empty keeps the stored one (it is never sent back).</param>
public sealed record GuardarConexionRequest(string Nombre, string Tipo, JsonElement? Configuracion, string? Secreto, bool Activa = true);

public sealed record ProbarConexionRequest(string? Destinatario);

/// <summary>
/// The admin side of the accounts with the outside world. Like credentials, hand-written: the secret is write-only (encrypted on the
/// way in, never in a response) and "no secret sent" means "keep the one stored".
/// </summary>
internal static class ConexionesEndpoints
{
    public static IEndpointRouteBuilder MapConexionesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/conexiones").RequireAuthorization(Permissions.ConexionesManage);
        group.MapGet("/tipos", ListarTipos);
        group.MapGet("/", ListarAsync);
        group.MapPost("/", CrearAsync);
        group.MapPut("/{id:guid}", ActualizarAsync);
        group.MapDelete("/{id:guid}", BorrarAsync);
        group.MapPost("/{id:guid}/probar", ProbarAsync);

        // Who configures notifications picks the account to send through: they see names and kinds, nothing else.
        endpoints.MapGet("/api/v1/conexiones/para-enviar", ListarParaEnviarAsync).RequireAuthorization(Permissions.FlujosManage);
        return endpoints;
    }

    private static IResult ListarTipos(TiposDeConexion tipos) =>
        Results.Ok(tipos.Todos.OrderBy(t => t.Nombre).Select(t => new TipoDeConexionDto(
            t.Codigo, t.Nombre, t.EtiquetaSecreto, t is ITipoQueEnviaMensajes,
            t.Campos.Select(c => new CampoDeConexionDto(c.Codigo, c.Etiqueta, c.Tipo, c.Obligatorio, c.PorDefecto, c.Ayuda)).ToList())));

    private static async Task<IResult> ListarAsync(AppDbContext db, TiposDeConexion tipos, CancellationToken ct)
    {
        var conexiones = await db.Set<Conexion>().AsNoTracking().OrderBy(c => c.Nombre).ToListAsync(ct);
        return Results.Ok(conexiones.Select(c => ADto(c, tipos)));
    }

    private static async Task<IResult> ListarParaEnviarAsync(AppDbContext db, TiposDeConexion tipos, CancellationToken ct)
    {
        var conexiones = await db.Set<Conexion>().AsNoTracking().Where(c => c.Activa).OrderBy(c => c.Nombre).ToListAsync(ct);
        return Results.Ok(conexiones
            .Where(c => tipos.Buscar(c.Tipo) is ITipoQueEnviaMensajes)
            .Select(c => new { c.Id, c.Nombre, c.Tipo, TipoNombre = tipos.Buscar(c.Tipo)!.Nombre }));
    }

    private static async Task<IResult> CrearAsync(
        GuardarConexionRequest request, AppDbContext db, TiposDeConexion tipos, ICredencialProtector protector, HttpContext http, CancellationToken ct)
    {
        if (Comprobar(request, tipos, out var tipo, out var configuracionJson) is { } incorrecta) return incorrecta;
        var nombre = request.Nombre.Trim();
        if (await db.Set<Conexion>().AnyAsync(c => c.Nombre == nombre, ct)) return ProblemResults.Conflict(http, "Ya existe una conexión con ese nombre.");

        var ahora = DateTimeOffset.UtcNow;
        var conexion = new Conexion
        {
            Nombre = nombre, Tipo = tipo.Codigo, ConfiguracionJson = configuracionJson, Activa = request.Activa, CreatedAt = ahora, UpdatedAt = ahora,
        };
        // The id is bound into the ciphertext, so it has to exist before encrypting.
        if (tipo.EtiquetaSecreto is not null && !string.IsNullOrEmpty(request.Secreto)) conexion.SecretoCifrado = protector.Proteger(request.Secreto, conexion.Id);

        db.Add(conexion);
        await db.SaveChangesAsync(ct);
        return Results.Ok(ADto(conexion, tipos));
    }

    private static async Task<IResult> ActualizarAsync(
        Guid id, GuardarConexionRequest request, AppDbContext db, TiposDeConexion tipos, ICredencialProtector protector, HttpContext http, CancellationToken ct)
    {
        var conexion = await db.Set<Conexion>().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (conexion is null) return ProblemResults.NotFound(http, "Conexión no encontrada.");
        if (Comprobar(request, tipos, out var tipo, out var configuracionJson) is { } incorrecta) return incorrecta;
        if (!string.Equals(tipo.Codigo, conexion.Tipo, StringComparison.OrdinalIgnoreCase))
        {
            return ProblemResults.Conflict(http, "Una conexión no cambia de tipo: crea otra.");
        }

        var nombre = request.Nombre.Trim();
        if (await db.Set<Conexion>().AnyAsync(c => c.Id != id && c.Nombre == nombre, ct)) return ProblemResults.Conflict(http, "Ya existe una conexión con ese nombre.");

        conexion.Nombre = nombre;
        conexion.ConfiguracionJson = configuracionJson;
        conexion.Activa = request.Activa;
        if (tipo.EtiquetaSecreto is not null && !string.IsNullOrEmpty(request.Secreto)) conexion.SecretoCifrado = protector.Proteger(request.Secreto, conexion.Id);
        conexion.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(ADto(conexion, tipos));
    }

    private static async Task<IResult> BorrarAsync(Guid id, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        var conexion = await db.Set<Conexion>().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (conexion is null) return ProblemResults.NotFound(http, "Conexión no encontrada.");

        db.Remove(conexion);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Something (a notification rule) still sends through it: the database refuses, and saying why is better than a 500.
            return ProblemResults.Conflict(http, "Hay reglas de notificación que usan esta conexión: cámbialas o desactiva la conexión.");
        }
        return Results.NoContent();
    }

    /// <summary>Sends a test message (to the address given, or to the account's own sender) and says what happened.</summary>
    private static async Task<IResult> ProbarAsync(
        Guid id, ProbarConexionRequest? request, AppDbContext db, EnviadorDeMensajes enviador, TiposDeConexion tipos, HttpContext http, CancellationToken ct)
    {
        var conexion = await db.Set<Conexion>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (conexion is null) return ProblemResults.NotFound(http, "Conexión no encontrada.");
        if (tipos.Buscar(conexion.Tipo) is not ITipoQueEnviaMensajes) return ProblemResults.Conflict(http, "Esta conexión no envía mensajes, no hay nada que probar.");

        using var configuracion = JsonDocument.Parse(conexion.ConfiguracionJson);
        var destinatario = string.IsNullOrWhiteSpace(request?.Destinatario)
            ? configuracion.RootElement.TryGetProperty("remitente", out var remitente) ? remitente.GetString() : null
            : request.Destinatario.Trim();
        if (destinatario is null || !MailAddress.TryCreate(destinatario, out _)) return ProblemResults.Conflict(http, "Indica a qué dirección mandar la prueba.");

        try
        {
            await enviador.EnviarAsync(conexion, new Mensaje(
                [destinatario],
                "Prueba de Viriato",
                $"Si lees esto, la conexión «{conexion.Nombre}» funciona: Viriato ya puede enviar avisos por ella."), ct);
            return Results.Ok(new { Enviado = true, Destinatario = destinatario });
        }
        catch (ConexionException ex)
        {
            return Results.Ok(new { Enviado = false, Destinatario = destinatario, Error = ex.Message });
        }
    }

    private static IResult? Comprobar(GuardarConexionRequest request, TiposDeConexion tipos, out ITipoDeConexion tipo, out string configuracionJson)
    {
        configuracionJson = "{}";
        tipo = null!;
        var errores = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Nombre) || request.Nombre.Trim().Length > 100) errores["nombre"] = ["El nombre es obligatorio, de 100 letras como mucho."];

        var encontrado = tipos.Buscar(request.Tipo ?? string.Empty);
        if (encontrado is null)
        {
            errores["tipo"] = [$"Tipo de conexión desconocido. Los que hay: {string.Join(", ", tipos.Todos.Select(t => t.Codigo))}."];
        }
        else
        {
            tipo = encontrado;
            var (json, problemas) = tipos.Normalizar(encontrado, request.Configuracion);
            if (problemas.Count > 0) errores["configuracion"] = problemas.ToArray();
            else configuracionJson = json!;
        }

        return errores.Count == 0 ? null : Results.ValidationProblem(errores);
    }

    private static ConexionDto ADto(Conexion c, TiposDeConexion tipos)
    {
        var tipo = tipos.Buscar(c.Tipo);
        using var configuracion = JsonDocument.Parse(c.ConfiguracionJson);
        return new ConexionDto(
            c.Id, c.Nombre, c.Tipo, tipo?.Nombre ?? c.Tipo, configuracion.RootElement.Clone(), c.SecretoCifrado is not null,
            tipo is ITipoQueEnviaMensajes, c.Activa, c.CreatedAt, c.UpdatedAt);
    }
}
