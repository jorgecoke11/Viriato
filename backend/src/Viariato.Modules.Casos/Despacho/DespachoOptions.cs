namespace Viariato.Modules.Casos.Despacho;

/// <summary>
/// The timings of the dispatcher, under the <c>Despacho</c> section of the API's configuration.
/// </summary>
public sealed class DespachoOptions
{
    public const string SectionName = "Despacho";

    /// <summary>How recently an idle robot must have called the API (it polls every few seconds) to count as
    /// there, and therefore to be waited for by the robots that rank below it. A robot that is off or gone for
    /// longer than this does not hold anybody up.</summary>
    public int VentanaConexionSegundos { get; set; } = 30;

    /// <summary>How often the background sweep looks for steps that have run past their service's maximum time. The
    /// sweep is what cuts them when nobody is asking about them; the dispatcher itself already stops counting a step
    /// past its time as running.</summary>
    public int BarridoSegundos { get; set; } = 15;

    public TimeSpan VentanaConexion => TimeSpan.FromSeconds(Math.Max(1, VentanaConexionSegundos));
}
