using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Viariato.ApiContracts;
using Viariato.Infrastructure;
using Viariato.Modules.RpaFleet.Auth;
using Viariato.Modules.RpaFleet.Domain;
using Viariato.Modules.RpaFleet.Security;
using Viariato.Shared.Http;

namespace Viariato.Modules.RpaFleet.Endpoints;

/// <summary>
/// The only place a stored password leaves Viriato in clear. ApiKey scheme only (never a user's JWT), and the
/// answer is the same 404 whether the credential does not exist, is inactive, or belongs to another Servicio —
/// a robot learns nothing about credentials it may not use. A switched-off Despliegue is refused too: turning
/// it off is the kill switch for its secrets as much as for its queue.
/// </summary>
internal static class CredencialesRobotEndpoints
{
    public static void MapCredencialesRobotEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/api/v1/rpa/credenciales")
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(ApiKeyDefaults.AuthenticationScheme)
                .RequireClaim(ApiKeyDefaults.DespliegueIdClaimType))
            .MapGet("/{nombre}", ObtenerAsync);
    }

    private static async Task<IResult> ObtenerAsync(
        string nombre,
        System.Security.Claims.ClaimsPrincipal principal,
        AppDbContext db,
        ICredencialProtector protector,
        ILoggerFactory loggerFactory,
        HttpContext http,
        CancellationToken ct)
    {
        var despliegue = await db.Set<Despliegue>().AsNoTracking().FirstOrDefaultAsync(d => d.Id == principal.GetDespliegueId(), ct);
        if (despliegue is null) return ProblemResults.NotFound(http, "Despliegue no encontrado.");
        if (!despliegue.Encendido) return ProblemResults.Conflict(http, "El despliegue está apagado.");

        // Names are stored lowercase; accept any casing from the robot.
        var clave = nombre.Trim().ToLowerInvariant();
        var credencial = await db.Set<Credencial>().FirstOrDefaultAsync(
            c => c.Nombre == clave && c.Activo && (c.ServicioId == null || c.ServicioId == despliegue.ServicioId), ct);
        if (credencial is null) return ProblemResults.NotFound(http, "Credencial no encontrada.");

        string password;
        try
        {
            password = protector.Revelar(credencial.PasswordCifrado, credencial.Id);
        }
        catch (CryptographicException ex)
        {
            // Never log the value. A failure here means the master key changed or the row was altered.
            loggerFactory.CreateLogger("Credenciales").LogError(ex, "No se pudo descifrar la credencial {Credencial}.", credencial.Nombre);
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "No se pudo descifrar la credencial",
                detail: "La credencial existe pero no se puede descifrar. Revisa la clave de cifrado del servidor.",
                instance: http.Request.Path);
        }

        credencial.UltimoAccesoAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.Pragma = "no-cache";
        return Results.Ok(new CredencialRobotDto(credencial.Nombre, credencial.Usuario, password));
    }
}
