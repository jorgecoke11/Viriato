namespace Viariato.Modules.Casos.Domain;

public enum EvidenciaTipo
{
    Screenshot,
    Video,
    ArchivoGenerado,
    DatosExtraidos,
    Otro,
}

/// <summary>Proof of what happened while an EjecucionPaso ran — a screenshot, a generated PDF, a raw
/// API response, extracted data. Distinct from a <see cref="Documento"/>, which is source material
/// attached to the Caso's expediente rather than something produced by processing it.</summary>
public sealed class Evidencia
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EjecucionPasoId { get; set; }

    public EjecucionPaso? EjecucionPaso { get; set; }

    /// <summary>Denormalized for direct "all evidence of this caso" queries.</summary>
    public Guid CasoId { get; set; }

    public EvidenciaTipo Tipo { get; set; }

    public required string Titulo { get; set; }

    /// <summary>jsonb, structured evidence content (e.g. extracted fields).</summary>
    public string? ContenidoJson { get; set; }

    /// <summary>Set when the evidence is itself a file (e.g. a screenshot).</summary>
    public Guid? DocumentoId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
