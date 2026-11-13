using System.Security.Claims;

namespace Viariato.Shared.Authorization;

/// <summary>
/// The claims Viriato puts in a user's access token, and how every module reads them. Kept here, next to
/// <see cref="Permissions"/>, so no module needs a reference to Users just to know who is calling or what they may do.
/// </summary>
public static class ClaimsDelUsuario
{
    /// <summary>The user's id. Same value as <c>JwtRegisteredClaimNames.Sub</c>, written out so Shared needs no JWT package.</summary>
    public const string Id = "sub";

    public const string Rol = "role";

    /// <summary>One claim per permission the user has (see <see cref="Permissions"/>).</summary>
    public const string Permiso = "perm";

    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(Id)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("The current principal has no 'sub' claim.");

        return Guid.Parse(value);
    }

    /// <summary>The same rule the permission policies apply, for checks that cannot be a policy (one per action, CRUD hooks).</summary>
    public static bool TienePermiso(this ClaimsPrincipal principal, string permiso) => principal.HasClaim(Permiso, permiso);
}
