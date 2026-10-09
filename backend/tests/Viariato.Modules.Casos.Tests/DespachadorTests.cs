using Viariato.Modules.Casos.Despacho;
using Viariato.Modules.RpaFleet.Domain;
using Xunit;

namespace Viariato.Modules.Casos.Tests;

public sealed class DespachadorTests
{
    private static readonly Guid S1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid S2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid S3 = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset T0 = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    /// <summary>One robot per service, with a step that has been waiting since <paramref name="minutos"/> past noon.</summary>
    private static Candidato Espera(Guid servicio, int minutos = 0) =>
        new(DespliegueId: Guid.NewGuid(), servicio, DetalleId: Guid.CreateVersion7(), T0.AddMinutes(minutos));

    private static Guid[] Servicios(Candidato[] candidatos, Guid[] orden, PoliticaDespacho politica, Guid? ultimo = null) =>
        Despachador.Ordenar(candidatos, orden, politica, ultimo).Select(c => c.ServicioId).ToArray();

    // ---------------------------------------------------------------- rank

    [Fact]
    public void Prioridad_RanksAServiceByItsPlaceInTheOrder()
    {
        Guid[] orden = [S2, S1, S3];

        Assert.Equal(0, Despachador.Rango(S2, orden, PoliticaDespacho.Prioridad, null));
        Assert.Equal(1, Despachador.Rango(S1, orden, PoliticaDespacho.Prioridad, null));
        Assert.Equal(2, Despachador.Rango(S3, orden, PoliticaDespacho.Prioridad, null));
    }

    [Fact]
    public void AServiceNobodyRanked_GoesAfterEveryRankedOne_InBothPolicies()
    {
        Guid[] orden = [S1];

        Assert.Equal(int.MaxValue, Despachador.Rango(S2, orden, PoliticaDespacho.Prioridad, null));
        Assert.Equal(int.MaxValue, Despachador.Rango(S2, orden, PoliticaDespacho.Turnos, S1));
    }

    [Theory]
    [InlineData(null, 0, 1, 2)] // nothing served yet: from the top
    [InlineData(0, 2, 0, 1)]    // S1 served last: S2 next, then S3, S1 waits its turn again
    [InlineData(1, 1, 2, 0)]    // S2 served last: S3 next, then S1
    [InlineData(2, 0, 1, 2)]    // S3 served last: back to the start
    public void Turnos_StartsAfterTheServiceServedLast_AndWrapsAround(int? ultimo, int rangoS1, int rangoS2, int rangoS3)
    {
        Guid[] orden = [S1, S2, S3];
        Guid? ultimoId = ultimo is { } i ? orden[i] : null;

        // The service right after the last one served is first (rank 0), the last one served is last in line.
        // The expected ranks are for S1, S2 and S3, in that order.
        Assert.Equal(
            [rangoS1, rangoS2, rangoS3],
            new[] { S1, S2, S3 }.Select(s => Despachador.Rango(s, orden, PoliticaDespacho.Turnos, ultimoId)).ToArray());
    }

    [Fact]
    public void Turnos_IfTheServiceServedLastIsNotListed_StartsFromTheTop()
    {
        Guid[] orden = [S1, S2];

        Assert.Equal(0, Despachador.Rango(S1, orden, PoliticaDespacho.Turnos, S3));
        Assert.Equal(1, Despachador.Rango(S2, orden, PoliticaDespacho.Turnos, S3));
    }

    // ---------------------------------------------------------------- choosing

    [Fact]
    public void Prioridad_TheHigherServiceGoesFirst_EvenIfItArrivedLater()
    {
        var antiguo = Espera(S2, minutos: 0);
        var reciente = Espera(S1, minutos: 30);

        var elegido = Despachador.Elegir([antiguo, reciente], [S1, S2], PoliticaDespacho.Prioridad, null);

        Assert.Equal(S1, elegido!.ServicioId);
    }

    [Fact]
    public void WithNoOrderConfigured_ItIsFirstComeFirstServed()
    {
        var segundo = Espera(S1, minutos: 10);
        var primero = Espera(S2, minutos: 5);
        var tercero = Espera(S3, minutos: 20);

        Assert.Equal([S2, S1, S3], Servicios([segundo, primero, tercero], [], PoliticaDespacho.Prioridad));
    }

    [Fact]
    public void AmongEqualRanks_TheOneThatWaitedLongestGoesFirst()
    {
        // Two robots of the same service (different processes), or two services nobody ranked.
        var reciente = Espera(S1, minutos: 9);
        var antiguo = Espera(S1, minutos: 1);

        Assert.Equal(antiguo, Despachador.Elegir([reciente, antiguo], [S1], PoliticaDespacho.Prioridad, null));
    }

    [Fact]
    public void WaitingTheSame_TheEarlierQueueEntryGoesFirst()
    {
        // Same instant: version-7 ids are time-ordered, so the earlier one sorts first.
        var primero = new Candidato(Guid.NewGuid(), S1, Guid.CreateVersion7(), T0);
        Thread.Sleep(5);
        var segundo = new Candidato(Guid.NewGuid(), S1, Guid.CreateVersion7(), T0);

        Assert.Equal(primero, Despachador.Elegir([segundo, primero], [S1], PoliticaDespacho.Prioridad, null));
    }

    [Fact]
    public void Prioridad_ListedServicesAlwaysBeatUnlistedOnes()
    {
        var sinLista = Espera(S3, minutos: 0);
        var enLista = Espera(S2, minutos: 60);

        Assert.Equal(S2, Despachador.Elegir([sinLista, enLista], [S1, S2], PoliticaDespacho.Prioridad, null)!.ServicioId);
    }

    [Fact]
    public void Turnos_AfterAServiceHasHadItsTurn_TheNextOneInTheListGoes()
    {
        Candidato[] candidatos = [Espera(S1, 0), Espera(S2, 1), Espera(S3, 2)];
        Guid[] orden = [S1, S2, S3];

        Assert.Equal([S2, S3, S1], Servicios(candidatos, orden, PoliticaDespacho.Turnos, ultimo: S1));
        Assert.Equal([S3, S1, S2], Servicios(candidatos, orden, PoliticaDespacho.Turnos, ultimo: S2));
        Assert.Equal([S1, S2, S3], Servicios(candidatos, orden, PoliticaDespacho.Turnos, ultimo: S3));
    }

    [Fact]
    public void Turnos_NobodyStarves_ASingleServiceWithWorkStillGetsItsTurn()
    {
        // S1 always has more work, but S2 has one step: after S1 was served, S2 goes — unlike Prioridad.
        Candidato[] candidatos = [Espera(S1, 0), Espera(S2, 99)];

        Assert.Equal(S2, Despachador.Elegir(candidatos, [S1, S2], PoliticaDespacho.Turnos, ultimoServicioId: S1)!.ServicioId);
        Assert.Equal(S1, Despachador.Elegir(candidatos, [S1, S2], PoliticaDespacho.Prioridad, ultimoServicioId: S1)!.ServicioId);
    }

    [Fact]
    public void Turnos_IfTheNextInRotationHasNoWork_TheFollowingOneDoes()
    {
        // S2 is next after S1 but has nothing waiting: it is skipped, not waited for.
        Candidato[] candidatos = [Espera(S1, 0), Espera(S3, 1)];

        Assert.Equal(S3, Despachador.Elegir(candidatos, [S1, S2, S3], PoliticaDespacho.Turnos, ultimoServicioId: S1)!.ServicioId);
    }

    [Fact]
    public void NothingWaiting_ChoosesNothing()
    {
        Assert.Null(Despachador.Elegir([], [S1, S2], PoliticaDespacho.Prioridad, null));
        Assert.Empty(Despachador.Ordenar([], [S1, S2], PoliticaDespacho.Turnos, S1));
    }

    [Fact]
    public void Ordenar_ReturnsEverythingNotJustTheWinner()
    {
        Candidato[] candidatos = [Espera(S2, 0), Espera(S1, 5), Espera(S2, 3)];

        var ordenados = Despachador.Ordenar(candidatos, [S1, S2], PoliticaDespacho.Prioridad, null);

        Assert.Equal(3, ordenados.Count);
        Assert.Equal([S1, S2, S2], ordenados.Select(c => c.ServicioId));
        Assert.True(ordenados[1].EnEsperaDesde < ordenados[2].EnEsperaDesde);
    }
}
