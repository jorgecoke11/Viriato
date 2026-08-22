namespace Viariato.Modules.Users.Authorization;

public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(Guid userId, string permission, CancellationToken ct);
}
