using Microsoft.AspNetCore.Http;
using Viariato.Shared.Authorization;
using Viariato.Shared.Http;

namespace Viariato.Modules.RpaFleet.Endpoints;

/// <summary>Same reasoning as Flujos' own claim-check helper: the generic Crud kit exposes a single
/// per-resource Authorize hook, so a plain claim check here is simpler than a DB-backed permission
/// checker for a module with only one admin-facing permission.</summary>
internal static class RpaFleetAuthorization
{
    public static Task<IResult?> RequireManageAsync(HttpContext http, CancellationToken ct)
    {
        var allowed = http.User.HasClaim("perm", Permissions.RpaManage);
        return Task.FromResult<IResult?>(allowed ? null : ProblemResults.Forbidden(http, "No tienes permiso para gestionar la flota de RPA."));
    }
}
