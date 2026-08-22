using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Viariato.Modules.Users.Auth;

internal static class CurrentUserExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The current principal has no 'sub' claim.");

        return Guid.Parse(value);
    }
}
