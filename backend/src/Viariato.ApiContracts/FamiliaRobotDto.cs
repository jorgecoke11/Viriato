namespace Viariato.ApiContracts;

/// <summary>A robot asking Viriato to open a family in the process it can create cases in.</summary>
/// <param name="Tipo">Name of one of the process's family types.</param>
/// <param name="Nombre">The family's name, as people see it (e.g. «Extracción BSH 18/11»).</param>
/// <param name="ParticipantesEsperados">If given, the family closes by itself when it has this many cases. Without it, close it with
/// <c>CerrarFamiliaAsync</c> once every case is in: a family that is never closed never ends.</param>
public sealed record CrearFamiliaRobotRequest(string Tipo, string Nombre, int? ParticipantesEsperados = null);

/// <summary>A case of a family as a robot sees it: how it ended and its data.</summary>
public sealed record MiembroDeFamiliaRobotDto(Guid Id, string Titulo, string Estado, string? EstadoNegocio, string? DatosJson);

/// <summary>A family as a robot sees it. <see cref="Estado"/> is <c>Abierta</c>, <c>Cerrada</c> or <c>Terminada</c>; the counts are
/// those of its end, null before. <see cref="Casos"/> is empty when the family is just created or closed.</summary>
public sealed record FamiliaRobotDto(
    Guid Id,
    string Nombre,
    string Tipo,
    string Estado,
    int? ParticipantesEsperados,
    int? Completados,
    int? Fallidos,
    int? Cancelados,
    IReadOnlyList<MiembroDeFamiliaRobotDto> Casos);
