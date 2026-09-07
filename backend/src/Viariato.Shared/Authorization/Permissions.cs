namespace Viariato.Shared.Authorization;

public static class Permissions
{
    public const string UsersRead = "users.read";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";
    public const string MarketsManage = "markets.manage";
    public const string FlujosRead = "flujos.read";
    public const string FlujosManage = "flujos.manage";
    public const string CasosRead = "casos.read";
    public const string CasosManage = "casos.manage";
    public const string CasosReview = "casos.review";
    public const string RpaManage = "rpa.manage";

    public static IReadOnlyList<string> All { get; } =
        [UsersRead, UsersManage, RolesManage, MarketsManage, FlujosRead, FlujosManage, CasosRead, CasosManage, CasosReview, RpaManage];
}
