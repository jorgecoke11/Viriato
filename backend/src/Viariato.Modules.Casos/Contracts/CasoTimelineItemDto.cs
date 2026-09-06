namespace Viariato.Modules.Casos.Contracts;

/// <summary>
/// One entry in a Caso's unified timeline — the three things the user actually wants to see
/// interleaved chronologically at the Caso level: business-status changes, document uploads, and
/// evidence. Technical per-step progress lives separately, inside each Ejecucion.
/// </summary>
public sealed record CasoTimelineItemDto(
    Guid Id,
    string Tipo,
    DateTimeOffset OccurredAt,
    string Titulo,
    string? EstadoCodigo,
    Guid? DocumentoId,
    string? EvidenciaTipo,
    string? ContenidoJson);

public static class CasoTimelineItemTipo
{
    public const string EstadoCambiado = "EstadoCambiado";
    public const string Documento = "Documento";
    public const string Evidencia = "Evidencia";
}
