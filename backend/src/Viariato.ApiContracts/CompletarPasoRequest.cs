namespace Viariato.ApiContracts;

public sealed record CompletarPasoRequest(string? ParametrosSalida);

/// <summary>What a robot tells Viriato while it runs a step, so that people can follow it: how far along it is, what it is doing, and
/// where its screen can be watched. Every field is optional and only the ones sent change (an empty text clears it).</summary>
/// <param name="Porcentaje">0 to 100.</param>
/// <param name="Mensaje">What it is doing now ("Añadiendo productos a la cesta"), up to 200 characters.</param>
/// <param name="VistaUrl">An http(s) address where the robot's screen can be watched live (up to 500 characters).</param>
public sealed record ReportarEnVivoRequest(int? Porcentaje = null, string? Mensaje = null, string? VistaUrl = null);
