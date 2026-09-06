namespace Viariato.Modules.Casos.Domain;

/// <summary>A file attached to a Caso's expediente (e.g. a DNI scan) — distinct from an
/// <see cref="Evidencia"/>, which proves what happened *during processing*, not source material.
/// Storage is abstracted behind <c>IDocumentStorage</c>; this row only ever holds an opaque key.</summary>
public sealed class Documento
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid CasoId { get; set; }

    /// <summary>Set when the document was produced/uploaded during a specific step.</summary>
    public Guid? EjecucionPasoId { get; set; }

    public required string Nombre { get; set; }

    public required string ContentType { get; set; }

    public long TamanoBytes { get; set; }

    /// <summary>Opaque key resolved via IDocumentStorage — never a filesystem path leaked into the domain.</summary>
    public required string StorageKey { get; set; }

    public string? Hash { get; set; }

    public Guid? UploadedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
