using Viariato.Modules.Casos.Domain;

namespace Viariato.Modules.Casos.Contracts;

public static class CasosDtoMapper
{
    private static EstadoNegocioDto? ToEstadoNegocioDto(this Caso caso) =>
        caso.EstadoNegocioActual is { } estado ? new EstadoNegocioDto(estado.Codigo, estado.Display, estado.EsFinal) : null;

    /// <param name="progresoPorcentaje">How far along the step a robot is running for this Caso says it is, if any is.</param>
    /// <param name="enVivo">Whether that robot's screen can be watched right now.</param>
    public static CasoListItemDto ToListItemDto(this Caso caso, int? progresoPorcentaje = null, bool enVivo = false) => new(
        caso.Id, caso.Titulo, caso.Estado.ToString(), caso.ToEstadoNegocioDto(), caso.TipoCaso?.Nombre, caso.FlujoId, caso.FlujoVersionId,
        caso.CreatedAt, caso.UpdatedAt, caso.CompletedAt, progresoPorcentaje, enVivo);

    public static CasoDetailDto ToDetailDto(this Caso caso, EjecucionDto? ejecucionActual, IReadOnlyList<CasoEventoDto> eventosRecientes) => new(
        caso.Id, caso.Titulo, caso.Estado.ToString(), caso.ToEstadoNegocioDto(), caso.TipoCaso?.Nombre, caso.FlujoId, caso.FlujoVersionId, caso.DatosJson,
        caso.CreatedAt, caso.UpdatedAt, caso.CompletedAt, ejecucionActual, eventosRecientes);

    public static EjecucionDto ToDto(this Ejecucion ejecucion, IReadOnlyList<EjecucionPasoDto> pasos) => new(
        ejecucion.Id, ejecucion.CasoId, ejecucion.FlujoVersionId, ejecucion.Estado.ToString(), ejecucion.PasoActualId,
        ejecucion.StartedAt, ejecucion.FinishedAt, pasos);

    /// <param name="detalle">The robot-side detail of the step if it is an RPA one: it holds the execution's priority and
    /// whether a robot has taken it yet.</param>
    /// <param name="servicioNombre">The service whose robots run the step, if it is an RPA one.</param>
    /// <param name="equipoNombre">The machine of the robot that took it, once one has.</param>
    public static EjecucionPasoDto ToDto(this EjecucionPaso paso, RpaEjecucionDetalle? detalle = null, string? servicioNombre = null, string? equipoNombre = null) => new(
        paso.Id, paso.EjecucionId, paso.FlujoPasoDefId, paso.TipoPaso.ToString(), paso.NumeroIntento, paso.Estado.ToString(),
        paso.TrabajoId, paso.ErrorMensaje, paso.StartedAt, paso.FinishedAt,
        detalle?.Prioridad, EnCola: detalle is { DespliegueId: null } && paso.Estado == EjecucionPasoEstado.EnProgreso,
        ServicioNombre: servicioNombre, EquipoNombre: equipoNombre,
        // What a robot says about a run only means something while the step is running (and a robot has it).
        ProgresoPorcentaje: EnMarcha(paso, detalle) ? detalle!.ProgresoPorcentaje : null,
        ProgresoMensaje: EnMarcha(paso, detalle) ? detalle!.ProgresoMensaje : null,
        VistaEnDirectoUrl: EnMarcha(paso, detalle) ? detalle!.VistaEnDirectoUrl : null);

    private static bool EnMarcha(EjecucionPaso paso, RpaEjecucionDetalle? detalle) =>
        detalle is { DespliegueId: not null } && paso.Estado == EjecucionPasoEstado.EnProgreso;

    public static CasoEventoDto ToDto(this CasoEvento evento) => new(
        evento.Id, evento.ActorUserId, evento.EjecucionId, evento.Accion.ToString(), evento.DetalleJson, evento.OccurredAt);

    /// <param name="pasoNombre">The step that produced the file, when a robot generated it rather than a person uploading it.</param>
    public static DocumentoDto ToDto(this Documento documento, string? pasoNombre = null) => new(
        documento.Id, documento.CasoId, documento.EjecucionPasoId, documento.Nombre, documento.ContentType,
        documento.TamanoBytes, documento.Hash, documento.CreatedAt,
        documento.Clasificaciones.OrderBy(c => c.PaginaDesde).Select(c => c.ToDto()).ToList(), pasoNombre);

    public static TipoDocumentoDto ToDto(this TipoDocumento tipo) => new(
        tipo.Id, tipo.Nombre, tipo.Descripcion, tipo.Activo, tipo.CreatedAt, tipo.UpdatedAt);

    public static DocumentoClasificacionDto ToDto(this DocumentoClasificacion clasificacion) => new(
        clasificacion.Id, clasificacion.DocumentoId, clasificacion.TipoDocumentoId,
        clasificacion.TipoDocumento?.Nombre ?? string.Empty, clasificacion.PaginaDesde, clasificacion.PaginaHasta,
        clasificacion.CreatedAt);

    public static EvidenciaDto ToDto(this Evidencia evidencia) => new(
        evidencia.Id, evidencia.EjecucionPasoId, evidencia.CasoId, evidencia.Tipo.ToString(), evidencia.Titulo,
        evidencia.ContenidoJson, evidencia.DocumentoId, evidencia.CreatedAt);

    public static RevisionHumanaDto ToDto(this RevisionHumana revision) => new(
        revision.Id, revision.EjecucionPasoId, revision.RevisorUserId, revision.Decision.ToString(), revision.Comentario, revision.ResolvedAt);
}
