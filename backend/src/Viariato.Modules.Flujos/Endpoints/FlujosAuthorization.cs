using Microsoft.AspNetCore.Http;
using Viariato.Shared.Http;

namespace Viariato.Modules.Flujos.Endpoints;

/// <summary>
/// CRUD-kit resources (<see cref="Domain.Flujo"/>, <see cref="Domain.AgenteDefinicion"/>) only expose
/// a single per-resource <c>Authorize</c> hook, so a plain claim check here — rather than a DB-backed
/// <c>IPermissionChecker</c> like Users uses — is what lets their GET endpoints require the weaker
/// "read" permission while their hand-written mutations require "manage" (see the group split in
/// <see cref="FlujosEndpoints"/>/<see cref="AgentesEndpoints"/>). No dependency on Modules.Users needed.
/// </summary>
internal static class FlujosAuthorization
{
    public static Task<IResult?> RequireClaimAsync(HttpContext http, string permission, CancellationToken ct)
    {
        var allowed = http.User.HasClaim("perm", permission);
        return Task.FromResult<IResult?>(allowed ? null : ProblemResults.Forbidden(http, "No tienes permiso para esta acción."));
    }
}
