namespace Viariato.ApiContracts;

/// <summary>A step adding to the business data of its Caso, so the next step (usually another robot) can work from it.</summary>
/// <param name="DatosJson">A JSON object. Each of its properties is written into the Caso's data, replacing the one with the same name
/// if there was one; the properties it does not mention stay as they were.</param>
public sealed record ActualizarDatosCasoRobotRequest(string DatosJson);
