using Viariato.Modules.Users.Seeding;
using Viariato.Shared.Authorization;
using Xunit;

namespace Viariato.Modules.Users.Tests.Authorization;

public sealed class PermisosPorDefectoTests
{
    private static readonly string[] DeConfiguracion =
    [
        Permissions.UsersRead, Permissions.UsersManage, Permissions.RolesManage, Permissions.MarketsManage,
        Permissions.FlujosRead, Permissions.FlujosManage, Permissions.FlujosCreadores,
        Permissions.RpaManage, Permissions.RpaDespacho, Permissions.RpaCredenciales,
    ];

    [Fact]
    public void ElRolUser_TieneTodoLoDeUsarLaPlataforma_YNadaDeConfigurarla()
    {
        foreach (var permiso in DeConfiguracion) Assert.DoesNotContain(permiso, Permissions.ParaElRolUser);
        foreach (var permiso in new[] { Permissions.CasosRead, Permissions.CasosCrear, Permissions.CasosMasivas, Permissions.CasosCancelar, Permissions.FlujosParametros })
        {
            Assert.Contains(permiso, Permissions.ParaElRolUser);
        }
    }

    [Fact]
    public void TodoPermisoDeLaListaExiste_YNoSeRepite()
    {
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct().Count());
        Assert.All(Permissions.ParaElRolUser, p => Assert.Contains(p, Permissions.All));
        Assert.All(Permissions.All, p => Assert.Matches(@"^[a-z]+\.[a-z]+$", p));
    }

    [Fact]
    public void UnRolSinPermisos_RecibeLaListaEntera()
    {
        var recibe = RoleAndPermissionSeeder.PermisosPorDefectoDelRolUser(elRolYaTienePermisos: false, new HashSet<string>());

        Assert.Equal(Permissions.ParaElRolUser, recibe);
    }

    [Fact]
    public void UnRolYaConfigurado_SoloRecibeLosPermisosNuevosDeLaLista_NuncaLosQueYaExistian()
    {
        var nuevos = new HashSet<string> { Permissions.CasosMasivas, Permissions.RpaDespacho };

        var recibe = RoleAndPermissionSeeder.PermisosPorDefectoDelRolUser(elRolYaTienePermisos: true, nuevos);

        // casos.masivas is new and in the list; rpa.despacho is new but not for the User role; casos.read existed (the
        // administrator may have taken it away on purpose) so it is not given back.
        Assert.Equal([Permissions.CasosMasivas], recibe);
    }

    [Fact]
    public void UnRolYaConfigurado_SinNingunPermisoNuevo_NoRecibeNada()
    {
        Assert.Empty(RoleAndPermissionSeeder.PermisosPorDefectoDelRolUser(elRolYaTienePermisos: true, new HashSet<string>()));
    }
}
