using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Viariato.Infrastructure;
using Viariato.Modules.Casos.Domain;
using Viariato.Modules.RpaFleet.Auth;
using Viariato.Shared.Http;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>
/// What a process's robots remember between cases (see <see cref="DatoDeProceso"/>). The process comes from the robot's key, never
/// from the request, so a robot can neither read nor overwrite another process's memory.
/// </summary>
internal static partial class AlmacenRobotEndpoints
{
    [GeneratedRegex(@"^[A-Za-z0-9._:\-]{1,200}$")]
    private static partial Regex ClaveValida();

    public static void MapAlmacenRobotEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/rpa/almacen")
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(ApiKeyDefaults.AuthenticationScheme)
                .RequireClaim(ApiKeyDefaults.DespliegueIdClaimType));

        group.MapGet("/{clave}", LeerAsync);
        group.MapPut("/{clave}", GuardarAsync);
        group.MapDelete("/{clave}", BorrarAsync);
    }

    private static IResult? ClaveIncorrecta(string clave, HttpContext http) =>
        ClaveValida().IsMatch(clave)
            ? null
            : Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Clave no válida",
                detail: "La clave son de 1 a 200 letras, números o los signos . _ : - (una URL, mejor como su huella).",
                instance: http.Request.Path);

    private static async Task<IResult> LeerAsync(string clave, ClaimsPrincipal principal, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (ClaveIncorrecta(clave, http) is { } incorrecta) return incorrecta;

        var flujoId = principal.GetFlujoId();
        var dato = await db.Set<DatoDeProceso>().AsNoTracking().FirstOrDefaultAsync(d => d.FlujoId == flujoId && d.Clave == clave, ct);
        if (dato is null) return ProblemResults.NotFound(http, "No hay nada guardado con esa clave.");

        using var valor = JsonDocument.Parse(dato.ValorJson);
        return Results.Ok(new { dato.Clave, Valor = valor.RootElement.Clone(), ActualizadoEn = dato.UpdatedAt });
    }

    private static async Task<IResult> GuardarAsync(string clave, ClaimsPrincipal principal, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (ClaveIncorrecta(clave, http) is { } incorrecta) return incorrecta;

        // The value is any JSON, read as it comes: the body itself is what is kept.
        using var lector = new StreamReader(http.Request.Body, Encoding.UTF8);
        var cuerpo = await lector.ReadToEndAsync(ct);
        if (Encoding.UTF8.GetByteCount(cuerpo) > DatoDeProceso.TamanoMaximoValor)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Demasiado grande",
                detail: $"Lo guardado no puede pasar de {DatoDeProceso.TamanoMaximoValor / 1024} KB.",
                instance: http.Request.Path);
        }

        string valorJson;
        try
        {
            using var documento = JsonDocument.Parse(cuerpo);
            valorJson = documento.RootElement.GetRawText();
        }
        catch (JsonException)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest, title: "No es JSON", detail: "El cuerpo tiene que ser un JSON válido.", instance: http.Request.Path);
        }

        var flujoId = principal.GetFlujoId();
        var dato = await db.Set<DatoDeProceso>().FirstOrDefaultAsync(d => d.FlujoId == flujoId && d.Clave == clave, ct);
        if (dato is null)
        {
            db.Add(new DatoDeProceso { FlujoId = flujoId, Clave = clave, ValorJson = valorJson, UpdatedAt = DateTimeOffset.UtcNow });
        }
        else
        {
            dato.ValorJson = valorJson;
            dato.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> BorrarAsync(string clave, ClaimsPrincipal principal, AppDbContext db, HttpContext http, CancellationToken ct)
    {
        if (ClaveIncorrecta(clave, http) is { } incorrecta) return incorrecta;

        var flujoId = principal.GetFlujoId();
        await db.Set<DatoDeProceso>().Where(d => d.FlujoId == flujoId && d.Clave == clave).ExecuteDeleteAsync(ct);
        return Results.NoContent();
    }
}
