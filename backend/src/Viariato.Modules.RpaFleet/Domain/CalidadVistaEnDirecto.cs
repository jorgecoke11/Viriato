using Viariato.ApiContracts;

namespace Viariato.Modules.RpaFleet.Domain;

/// <summary>
/// How a Despliegue's robot shows its screen live (the "En directo" panel of a Caso). Decided by the platform, not by each robot:
/// watching costs the robot CPU (on a 1.5-CPU container, a viewer at full rate took about a quarter of a core), so a slow robot can
/// be given a lighter view or none at all.
/// </summary>
public enum CalidadVistaEnDirecto
{
    /// <summary>No live view: Viriato does not show it and the robot does not serve it.</summary>
    Apagada = 0,

    /// <summary>About two pictures a second, heavily compressed. What a robot starts with.</summary>
    Baja = 1,

    Media = 2,

    /// <summary>As smooth as the viewer allows — how it was before the platform decided it.</summary>
    Alta = 3,
}

/// <summary>What each quality means in numbers, in one place: the robot reads the refresh rate (it is the screen server's job)
/// and the viewer the picture quality and compression (noVNC's own <c>quality</c> and <c>compression</c>, 0–9).</summary>
public static class VistaEnDirectoPresets
{
    public static VistaEnDirectoDto? ParaElRobot(CalidadVistaEnDirecto calidad) => calidad switch
    {
        CalidadVistaEnDirecto.Baja => new(nameof(CalidadVistaEnDirecto.Baja), MilisegundosEntreImagenes: 500, CalidadImagen: 2, Compresion: 9),
        CalidadVistaEnDirecto.Media => new(nameof(CalidadVistaEnDirecto.Media), MilisegundosEntreImagenes: 200, CalidadImagen: 5, Compresion: 6),
        // x11vnc's and noVNC's own defaults.
        CalidadVistaEnDirecto.Alta => new(nameof(CalidadVistaEnDirecto.Alta), MilisegundosEntreImagenes: 20, CalidadImagen: 6, Compresion: 2),
        _ => null,
    };

    /// <summary>
    /// The address the interface opens to watch the screen, with the picture quality of <paramref name="calidad"/>. Null when the view
    /// is off. Only a noVNC page (<c>…/vnc.html</c>) understands those options; any other viewer's address is left as the robot said.
    /// </summary>
    public static string? DireccionParaVer(string? vistaUrl, CalidadVistaEnDirecto calidad)
    {
        if (string.IsNullOrWhiteSpace(vistaUrl) || ParaElRobot(calidad) is not { } vista) return null;
        if (!Uri.TryCreate(vistaUrl, UriKind.Absolute, out var uri) || !uri.AbsolutePath.EndsWith("vnc.html", StringComparison.OrdinalIgnoreCase))
        {
            return vistaUrl;
        }

        var parametros = System.Web.HttpUtility.ParseQueryString(uri.Query);
        parametros["quality"] = vista.CalidadImagen.ToString(System.Globalization.CultureInfo.InvariantCulture);
        parametros["compression"] = vista.Compresion.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new UriBuilder(uri) { Query = parametros.ToString() }.Uri.ToString();
    }
}

/// <summary>Whether a Despliegue's robot records each run as a video (uploaded to the Caso as evidence). Recording reads the screen the
/// whole time a case runs, so it costs CPU even when nobody watches.</summary>
public enum GrabacionDeVideo
{
    /// <summary>The robot's own setting decides (<c>Grabacion__Activa</c> in the BSH extractor), as before the platform did.</summary>
    SegunRobot = 0,

    Activa = 1,

    Apagada = 2,
}
