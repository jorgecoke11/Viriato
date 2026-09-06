using System.Security.Claims;

namespace Viariato.Modules.Casos.Endpoints;

/// <summary>Own copy of the same tiny "sub" claim reader Users has (Auth/CurrentUserExtensions.cs) —
/// duplicated rather than referenced so Casos doesn't need a ProjectReference to Modules.Users just
/// for this. "sub" is read as a literal string instead of via JwtRegisteredClaimNames.Sub (same
/// value) to avoid pulling in System.IdentityModel.Tokens.Jwt for one constant.</summary>
internal static class CurrentUserExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("sub")
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The current principal has no 'sub' claim.");

        return Guid.Parse(value);
    }
}
