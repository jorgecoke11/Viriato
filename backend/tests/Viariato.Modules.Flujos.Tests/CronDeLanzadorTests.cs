using Viariato.Modules.Flujos.Esquemas;
using Xunit;

namespace Viariato.Modules.Flujos.Tests;

public sealed class CronDeLanzadorTests
{
    private static readonly TimeZoneInfo NuevaYork = CronDeLanzador.BuscarZona("America/New_York")!;
    private static readonly TimeSpan DosHoras = TimeSpan.FromHours(2);

    private static IReadOnlyList<Cronos.CronExpression> Cron(string texto)
    {
        Assert.True(CronDeLanzador.TryLeer(texto, out var expresiones, out var error), error);
        return expresiones;
    }

    [Fact]
    public void Lee_UnaExpresionPorLinea_YTambienConPuntoYComa()
    {
        Assert.Equal(3, Cron("0 10 * * 1-5\n30 12 * * 1-5\n45 15 * * 1-5").Count);
        Assert.Equal(2, Cron("0 10 * * *; 0 11 * * *").Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("todos los dias a las 10")]
    [InlineData("0 25 * * *")]
    [InlineData("0 10 * *")]
    public void LoQueNoEsUnCron_SeRechaza_ConUnMensaje(string texto)
    {
        Assert.False(CronDeLanzador.TryLeer(texto, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void ElMomento_SeLeeEnLaZonaDelMercado()
    {
        // Tuesday 2030-01-15: 10:00 in New York is 15:00 UTC.
        var cron = Cron("0 10 * * 1-5");
        var ayer = new DateTimeOffset(2030, 1, 14, 20, 0, 0, TimeSpan.Zero);

        Assert.Null(CronDeLanzador.Pendiente(new DateTimeOffset(2030, 1, 15, 14, 59, 0, TimeSpan.Zero), NuevaYork, cron, ayer, DosHoras));
        Assert.Equal(new DateTimeOffset(2030, 1, 15, 15, 0, 0, TimeSpan.Zero), CronDeLanzador.Pendiente(new DateTimeOffset(2030, 1, 15, 15, 1, 0, TimeSpan.Zero), NuevaYork, cron, ayer, DosHoras));
    }

    [Fact]
    public void ElMismoMomento_NoDisparaDosVeces()
    {
        var cron = Cron("0 10 * * *");
        var momento = new DateTimeOffset(2030, 1, 15, 15, 0, 0, TimeSpan.Zero);

        Assert.Null(CronDeLanzador.Pendiente(momento.AddMinutes(5), NuevaYork, cron, momento, DosHoras));
    }

    [Fact]
    public void PasadaLaTolerancia_SeSalta()
    {
        var cron = Cron("0 10 * * *");
        var momento = new DateTimeOffset(2030, 1, 15, 15, 0, 0, TimeSpan.Zero);

        Assert.Null(CronDeLanzador.Pendiente(momento.AddHours(3), NuevaYork, cron, momento.AddDays(-1), DosHoras));
        Assert.NotNull(CronDeLanzador.Pendiente(momento.AddHours(1), NuevaYork, cron, momento.AddDays(-1), DosHoras));
    }

    [Fact]
    public void ConVariosMomentosPerdidos_SoloCuentaElUltimo()
    {
        var cron = Cron("*/10 * * * *");
        var ahora = new DateTimeOffset(2030, 1, 15, 12, 35, 0, TimeSpan.Zero);

        Assert.Equal(new DateTimeOffset(2030, 1, 15, 12, 30, 0, TimeSpan.Zero), CronDeLanzador.Pendiente(ahora, CronDeLanzador.BuscarZona("UTC")!, cron, ahora.AddHours(-1), DosHoras));
    }

    [Fact]
    public void VariasExpresiones_DisparanCuandoCualquieraCoincide()
    {
        var cron = Cron("0 10 * * 1-5\n30 12 * * 1-5\n45 15 * * 1-5");
        var ultimo = new DateTimeOffset(2030, 1, 15, 15, 0, 0, TimeSpan.Zero); // the 10:00 ET already fired

        Assert.Equal(new DateTimeOffset(2030, 1, 15, 17, 30, 0, TimeSpan.Zero), CronDeLanzador.Pendiente(new DateTimeOffset(2030, 1, 15, 17, 31, 0, TimeSpan.Zero), NuevaYork, cron, ultimo, DosHoras));
        Assert.Equal(new DateTimeOffset(2030, 1, 15, 17, 30, 0, TimeSpan.Zero), CronDeLanzador.Proximo(new DateTimeOffset(2030, 1, 15, 16, 0, 0, TimeSpan.Zero), NuevaYork, cron));
    }

    [Fact]
    public void ElProximo_SaltaElFinDeSemana()
    {
        var cron = Cron("0 10 * * 1-5");
        // Friday 2030-01-18 after 10:00 ET: the next one is Monday the 21st.
        var proximo = CronDeLanzador.Proximo(new DateTimeOffset(2030, 1, 18, 16, 0, 0, TimeSpan.Zero), NuevaYork, cron);

        Assert.Equal(new DateTimeOffset(2030, 1, 21, 15, 0, 0, TimeSpan.Zero), proximo);
    }

    [Fact]
    public void ConElCambioDeHora_LaMismaHoraLocalSiguePasandoALaMismaHoraLocal()
    {
        var cron = Cron("0 10 * * *");

        // Before the clocks go forward (2030-03-10) New York is UTC-5; after, UTC-4.
        Assert.Equal(new DateTimeOffset(2030, 3, 9, 15, 0, 0, TimeSpan.Zero), CronDeLanzador.Proximo(new DateTimeOffset(2030, 3, 9, 12, 0, 0, TimeSpan.Zero), NuevaYork, cron));
        Assert.Equal(new DateTimeOffset(2030, 3, 11, 14, 0, 0, TimeSpan.Zero), CronDeLanzador.Proximo(new DateTimeOffset(2030, 3, 11, 12, 0, 0, TimeSpan.Zero), NuevaYork, cron));
    }

    [Fact]
    public void UnaZonaQueNoExiste_NoSeEncuentra()
    {
        Assert.Null(CronDeLanzador.BuscarZona("Mars/Olympus"));
        Assert.Null(CronDeLanzador.BuscarZona(""));
        Assert.NotNull(CronDeLanzador.BuscarZona("Europe/Madrid"));
    }
}
