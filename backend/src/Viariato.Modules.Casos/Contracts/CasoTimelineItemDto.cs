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
    string? ContenidoJson,
    string? PasoNombre = null,
    string? NombreArchivo = null,
    string? ContentType = null,
    long? TamanoBytes = null);

public static class CasoTimelineItemTipo
{
    public const string EstadoCambiado = "EstadoCambiado";
    public const string Documento = "Documento";
    public const string Evidencia = "Evidencia";
    /// <summary>A notification that went out (EstadoCodigo "enviado") or could not (EstadoCodigo "fallido"); ContenidoJson has the subject,
    /// the recipients and, if it failed, why.</summary>
    public const string Aviso = "Aviso";
    /// <summary>What a reprocessing rule did with the case: EstadoCodigo "programado" (a step will run again), "agotado" (the limit ran out)
    /// or "fallido" (it could not run).</summary>
    public const string Reprocesamiento = "Reprocesamiento";
}
