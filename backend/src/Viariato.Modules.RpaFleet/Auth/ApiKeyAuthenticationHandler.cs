using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Viariato.Modules.RpaFleet.Auth;

/// <summary>
/// Authenticates a Despliegue by its X-Api-Key header instead of a JWT. Registered as an additional
/// scheme alongside the default Bearer one (see RpaFleetAuthenticationExtensions) — only endpoints
/// that explicitly opt in via AddAuthenticationSchemes(ApiKeyDefaults.AuthenticationScheme) ever run
/// this; every existing user-facing endpoint is completely unaffected.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IDespliegueApiKeyValidator validator)
    : AuthenticationHandler<ApiKeyAuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyDefaults.HeaderName, out var values))
        {
            return AuthenticateResult.NoResult();
        }

        var rawKey = values.ToString();
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return AuthenticateResult.NoResult();
        }

        var identity = await validator.ValidateAsync(rawKey, Context.RequestAborted);
        if (identity is null)
        {
            return AuthenticateResult.Fail("Clave de API inválida.");
        }

        var claims = new[]
        {
            new Claim(ApiKeyDefaults.DespliegueIdClaimType, identity.DespliegueId.ToString()),
            new Claim(ApiKeyDefaults.EquipoIdClaimType, identity.EquipoId.ToString()),
            new Claim(ApiKeyDefaults.ServicioIdClaimType, identity.ServicioId.ToString()),
            new Claim(ApiKeyDefaults.FlujoIdClaimType, identity.FlujoId.ToString()),
        };
        var claimsIdentity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(claimsIdentity), Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}
