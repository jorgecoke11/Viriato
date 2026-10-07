namespace Viariato.ApiContracts;

/// <summary>What a worker gets back after successfully claiming a pending step from the queue.
/// <c>ParametrosEntrada</c> is the step's own configuration; <c>DatosCasoJson</c> is the Caso's business
/// data (what the user filled in when creating it), which is what most robots actually work from.
/// <c>TipoCaso</c> is the name of the Caso's type within its process (null when it has none): a robot that only
/// makes sense for one kind of Caso can check it before doing anything.</summary>
public sealed record EjecucionAsignadaDto(
    Guid EjecucionPasoId,
    Guid CasoId,
    string CasoTitulo,
    string FlujoNombre,
    string AplicacionObjetivo,
    string? ParametrosEntrada,
    string? DatosCasoJson,
    string? TipoCaso = null);
