using System.Security.Claims;

namespace Viariato.Modules.Cartera.Endpoints;

/// <summary>Own copy of the tiny "sub" claim reader the other modules keep, so Cartera needs no reference to Users for one claim.</summary>
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
