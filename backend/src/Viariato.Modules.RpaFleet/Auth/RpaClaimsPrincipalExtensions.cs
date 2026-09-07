using System.Security.Claims;

namespace Viariato.Modules.RpaFleet.Auth;

public static class RpaClaimsPrincipalExtensions
{
    public static Guid GetDespliegueId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ApiKeyDefaults.DespliegueIdClaimType)!);

    public static Guid GetServicioId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ApiKeyDefaults.ServicioIdClaimType)!);

    public static Guid GetFlujoId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ApiKeyDefaults.FlujoIdClaimType)!);
}
