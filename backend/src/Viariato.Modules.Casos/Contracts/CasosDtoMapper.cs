using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.Contracts;

public static class CasosDtoMapper
{
    private static EstadoNegocioDto? ToEstadoNegocioDto(this Caso caso) =>
        caso.EstadoNegocioActual is { } estado ? new EstadoNegocioDto(estado.Codigo, estado.Display) : null;

    public static CasoListItemDto ToListItemDto(this Caso caso) => new(
        caso.Id, caso.Titulo, caso.Estado.ToString(), caso.ToEstadoNegocioDto(), caso.TipoCaso?.Nombre, caso.FlujoId, caso.FlujoVersionId,
        caso.CreatedAt, caso.UpdatedAt, caso.CompletedAt);

    public static CasoDetailDto ToDetailDto(this Caso caso, EjecucionDto? ejecucionActual, IReadOnlyList<CasoEventoDto> eventosRecientes) => new(
        caso.Id, caso.Titulo, caso.Estado.ToString(), caso.ToEstadoNegocioDto(), caso.TipoCaso?.Nombre, caso.FlujoId, caso.FlujoVersionId, caso.DatosJson,
        caso.CreatedAt, caso.UpdatedAt, caso.CompletedAt, ejecucionActual, eventosRecientes);

    public static EjecucionDto ToDto(this Ejecucion ejecucion, IReadOnlyList<EjecucionPasoDto> pasos) => new(
        ejecucion.Id, ejecucion.CasoId, ejecucion.FlujoVersionId, ejecucion.Estado.ToString(), ejecucion.PasoActualId,
        ejecucion.StartedAt, ejecucion.FinishedAt, pasos);

    public static EjecucionPasoDto ToDto(this EjecucionPaso paso) => new(
        paso.Id, paso.EjecucionId, paso.FlujoPasoDefId, paso.TipoPaso.ToString(), paso.NumeroIntento, paso.Estado.ToString(),
        paso.TrabajoId, paso.ErrorMensaje, paso.StartedAt, paso.FinishedAt);

    public static CasoEventoDto ToDto(this CasoEvento evento) => new(
        evento.Id, evento.ActorUserId, evento.EjecucionId, evento.Accion.ToString(), evento.DetalleJson, evento.OccurredAt);

    public static DocumentoDto ToDto(this Documento documento) => new(
        documento.Id, documento.CasoId, documento.EjecucionPasoId, documento.Nombre, documento.ContentType,
        documento.TamanoBytes, documento.Hash, documento.CreatedAt);

    public static EvidenciaDto ToDto(this Evidencia evidencia) => new(
        evidencia.Id, evidencia.EjecucionPasoId, evidencia.CasoId, evidencia.Tipo.ToString(), evidencia.Titulo,
        evidencia.ContenidoJson, evidencia.DocumentoId, evidencia.CreatedAt);

    public static RevisionHumanaDto ToDto(this RevisionHumana revision) => new(
        revision.Id, revision.EjecucionPasoId, revision.RevisorUserId, revision.Decision.ToString(), revision.Comentario, revision.ResolvedAt);
}
