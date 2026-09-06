namespace Viariato.Modules.Casos.Domain;

/// <summary>1:1 structured detail for an EjecucionPaso whose TipoPaso is Rpa.</summary>
public sealed class RpaEjecucionDetalle
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid EjecucionPasoId { get; set; }

    public EjecucionPaso? EjecucionPaso { get; set; }

    public required string AplicacionObjetivo { get; set; }

    public string? WorkerId { get; set; }

    public string? ParametrosEntrada { get; set; }

    public string? ParametrosSalida { get; set; }
}
