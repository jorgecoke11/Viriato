namespace Viariato.ApiContracts;

/// <summary>What GET /api/v1/rpa/despliegue reports back — "am I switched on?" plus enough context
/// for a robot's own logs, and how the platform wants the robot's screen handled.</summary>
/// <param name="Pantalla">Null only from a Viriato older than this field: the robot then keeps its own settings.</param>
public sealed record DespliegueEstadoDto(
    bool Encendido, string EquipoNombre, string ServicioNombre, string FlujoNombre, PantallaDelRobotDto? Pantalla = null);

/// <summary>What the platform decided about the robot's screen for this Despliegue.</summary>
/// <param name="Vista">The live view, or null when it is switched off: the robot should not serve it nor report its address.</param>
/// <param name="GrabarVideo">Whether to record each run as a video; null leaves it to the robot's own setting.</param>
public sealed record PantallaDelRobotDto(VistaEnDirectoDto? Vista, bool? GrabarVideo);

/// <param name="Calidad">The name of the level (Baja, Media, Alta), for logs.</param>
/// <param name="MilisegundosEntreImagenes">How often the screen server looks for changes (x11vnc's <c>-wait</c>/<c>-defer</c>):
/// the main lever on the CPU the view costs.</param>
/// <param name="CalidadImagen">Picture quality the viewer asks for, 0–9 (noVNC's <c>quality</c>).</param>
/// <param name="Compresion">Compression the viewer asks for, 0–9 (noVNC's <c>compression</c>).</param>
public sealed record VistaEnDirectoDto(string Calidad, int MilisegundosEntreImagenes, int CalidadImagen, int Compresion);
