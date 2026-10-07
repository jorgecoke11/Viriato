namespace Viriato.Rpa.Client.Selenium;

/// <summary>Browser settings shared by robots; bind it from a <c>Navegador</c> configuration section.</summary>
public sealed class NavegadorOptions
{
    /// <summary>Where Edge saves downloads. Empty = a folder under the system temp directory.</summary>
    public string CarpetaDescargas { get; set; } = string.Empty;

    /// <summary>Kill every msedge/msedgewebview2 process when the browser is closed (and let the robot do so
    /// before opening it). Only for a machine dedicated to the robot: it also closes your own Edge windows.</summary>
    public bool MatarProcesosEdge { get; set; }

    public string CarpetaDescargasEfectiva() =>
        string.IsNullOrWhiteSpace(CarpetaDescargas)
            ? Path.Combine(Path.GetTempPath(), "rpa-descargas")
            : CarpetaDescargas;
}
