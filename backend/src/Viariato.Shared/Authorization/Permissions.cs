namespace Viariato.Shared.Authorization;

/// <summary>
/// Every permission the platform knows. They are written as <c>module.action</c>; the seeder creates them in the database at
/// startup and gives the Admin role all of them, so declaring one here is all it takes for an administrator to have it.
/// Two kinds: the ones for *using* the platform (reading cases, creating them, acting on them) and the ones for *managing* it
/// (processes, robots, credentials, users). The <c>User</c> role gets the first kind (see <see cref="ParaElRolUser"/>).
/// </summary>
public static class Permissions
{
    // ---- users and roles
    public const string UsersRead = "users.read";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";

    public const string MarketsManage = "markets.manage";

    // ---- processes (configuration)
    public const string FlujosRead = "flujos.read";
    /// <summary>Create and configure processes: versions, steps, states, case types, assignments, storage; and start a case by hand.</summary>
    public const string FlujosManage = "flujos.manage";
    /// <summary>Configure the case creators of a process.</summary>
    public const string FlujosCreadores = "flujos.creadores";
    /// <summary>Change, from the dashboard, the parameters of the processes one is assigned to that the process's author opened up.</summary>
    public const string FlujosParametros = "flujos.parametros";

    // ---- cases (use)
    /// <summary>See the dashboard, the list of cases and a case, of the processes one is assigned to.</summary>
    public const string CasosRead = "casos.read";
    /// <summary>Operate a case: pause, resume, reprocess, change its data, add documents and evidence.</summary>
    public const string CasosManage = "casos.manage";
    public const string CasosReview = "casos.review";
    /// <summary>Create a case by picking one of the process's creators.</summary>
    public const string CasosCrear = "casos.crear";
    /// <summary>Cancel a case, or one execution of it.</summary>
    public const string CasosCancelar = "casos.cancelar";
    /// <summary>Change where an execution stands in the queue of its service.</summary>
    public const string CasosPrioridad = "casos.prioridad";
    /// <summary>Use the bulk-actions screen (each action also asks for its own permission).</summary>
    public const string CasosMasivas = "casos.masivas";
    /// <summary>Download the documents of many cases at once, as a zip.</summary>
    public const string CasosDescargar = "casos.descargar";

    // ---- robots (configuration)
    public const string RpaManage = "rpa.manage";
    /// <summary>Configure how each machine decides what goes first, its templates, and see its queue.</summary>
    public const string RpaDespacho = "rpa.despacho";
    /// <summary>Create and edit the credentials the robots ask for (secrets: kept apart from the rest of the robot fleet).</summary>
    public const string RpaCredenciales = "rpa.credenciales";

    public static IReadOnlyList<string> All { get; } =
    [
        UsersRead, UsersManage, RolesManage,
        MarketsManage,
        FlujosRead, FlujosManage, FlujosCreadores, FlujosParametros,
        CasosRead, CasosManage, CasosReview, CasosCrear, CasosCancelar, CasosPrioridad, CasosMasivas, CasosDescargar,
        RpaManage, RpaDespacho, RpaCredenciales,
    ];

    /// <summary>What the <c>User</c> role starts with: everything to work the cases of the processes one is assigned to — see them,
    /// create them, act on them, cancel and reprioritise them, use the bulk actions, change the parameters opened to them — and
    /// nothing to configure the platform. An administrator can change it afterwards like any other role.</summary>
    public static IReadOnlyList<string> ParaElRolUser { get; } =
        [CasosRead, CasosManage, CasosReview, CasosCrear, CasosCancelar, CasosPrioridad, CasosMasivas, CasosDescargar, FlujosParametros];
}
