using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Viariato.Shared.Authorization;
using Viariato.Shared.Options;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>
/// The door of a robot's live screen. That screen is served by Viriato's own address, under <c>/vista/</c> (Caddy passes it on to the
/// robot's noVNC), and the browser loads that page, its files and its websocket by itself, without the bearer token API calls carry.
/// So the door is a cookie: a signed-in person with casos.read asks for a pass, the pass goes into an HttpOnly cookie that is only
/// sent to <c>/vista</c>, and Caddy asks <c>comprobar</c> before letting any request through. Without it, anyone who knew the address
/// would watch the robot type the portal's user and password.
/// </summary>
internal static class VistaEnDirectoEndpoints
{
    public const string CookieName = "viriato_vista";
    private const string CookiePath = "/vista";

    /// <summary>The websocket is only checked when it opens, so this just has to outlast loading the page; the front asks again
    /// every time it shows a screen.</summary>
    private static readonly TimeSpan Duracion = TimeSpan.FromHours(1);

    public static void MapVistaEnDirectoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/casos/vista/pase", DarPase).RequireAuthorization(Permissions.CasosRead);
        // Caddy's forward_auth calls this for every request to /vista/*: it has no token, only the cookie.
        endpoints.MapGet("/api/v1/casos/vista/comprobar", Comprobar).AllowAnonymous();
    }

    private static IResult DarPase(IOptions<JwtOptions> jwt, IHostEnvironment env, HttpContext http)
    {
        var pase = PaseDeVista.Emitir(jwt.Value.SigningKey, http.User.GetUserId(), DateTimeOffset.UtcNow.Add(Duracion));
        http.Response.Cookies.Append(CookieName, pase, new CookieOptions
        {
            HttpOnly = true,
            // Same rule as the session cookie: HTTPS everywhere but the developer's own machine.
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = CookiePath,
            MaxAge = Duracion,
        });
        return Results.NoContent();
    }

    private static IResult Comprobar(IOptions<JwtOptions> jwt, HttpContext http) =>
        http.Request.Cookies.TryGetValue(CookieName, out var pase) && PaseDeVista.EsValido(jwt.Value.SigningKey, pase, DateTimeOffset.UtcNow)
            ? Results.NoContent()
            : Results.Unauthorized();
}

/// <summary>
/// A pass to watch robots' screens until a given time: <c>{user}.{expires}.{signature}</c>, signed with a key derived from the JWT
/// signing key (so it survives restarts and works on every replica) but different from it, so a pass is never an access token.
/// </summary>
internal static class PaseDeVista
{
    private static readonly byte[] Proposito = "viriato:vista-en-directo"u8.ToArray();

    public static string Emitir(string signingKey, Guid userId, DateTimeOffset caduca)
    {
        var contenido = $"{userId:N}.{caduca.ToUnixTimeSeconds()}";
        return $"{contenido}.{Convert.ToHexString(Firmar(signingKey, contenido))}";
    }

    public static bool EsValido(string signingKey, string? pase, DateTimeOffset ahora)
    {
        if (string.IsNullOrEmpty(pase)) return false;

        var partes = pase.Split('.');
        if (partes.Length != 3 || !Guid.TryParseExact(partes[0], "N", out _) || !long.TryParse(partes[1], out var caduca)) return false;
        if (DateTimeOffset.FromUnixTimeSeconds(caduca) <= ahora) return false;

        byte[] firma;
        try
        {
            firma = Convert.FromHexString(partes[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(firma, Firmar(signingKey, $"{partes[0]}.{partes[1]}"));
    }

    private static byte[] Firmar(string signingKey, string contenido)
    {
        var clave = HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), Proposito);
        return HMACSHA256.HashData(clave, Encoding.UTF8.GetBytes(contenido));
    }
}
