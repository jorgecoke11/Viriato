using System.Security.Claims;

namespace Viariato.Modules.Flujos.Endpoints;

/// <summary>Own copy of the same tiny "sub" claim reader Users/Casos have — duplicated rather than
/// referenced so Flujos doesn't need a ProjectReference to Modules.Users just for this.</summary>
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
