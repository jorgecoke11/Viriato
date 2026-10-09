import { apiFetch, apiFetchArchivo } from '../../lib/apiClient'
import { CABECERA_DE_RESUMEN, leerResumenDeZip, nombreDeContentDisposition } from './resumenDeZip'
import type { ResultadoMasivo } from '../../lib/accionesMasivas'
import type { PagedResult } from '../../lib/types'

export interface FlujoAsignadoDto {
  id: string
  nombre: string
  descripcion: string | null
  versionActivaId: string | null
  numeroVersionActiva: number | null
  activo: boolean
  createdAt: string
  updatedAt: string
}

export interface EstadoConteoDto {
  codigo: string | null
  display: string
  orden: number
  count: number
  esFinal: boolean
}

export interface TipoCasoConteoDto {
  tipoCasoId: string | null
  nombre: string
  orden: number
  enCurso: number
  finalizados: number
  porEstado: EstadoConteoDto[]
  /** Of its casos, how many a robot is running right now. */
  enEjecucion: number
  /** Of its casos, how many wait in the queue for a robot to take them. */
  pendientes: number
  /** Of its casos, how many are just started, paused or waiting for a person: not running, not queued, not over. */
  detenidos: number
}

export interface FlujoResumenDto {
  flujoId: string
  flujoNombre: string
  total: number
  porTipo: TipoCasoConteoDto[]
  /** How many of the process's parameters the person may change from its card. */
  parametrosEditables: number
}

export interface EstadoNegocioDto {
  codigo: string
  display: string
  /** The process says the case is over once it reaches this estado. */
  esFinal: boolean
}

export type CasoEstado =
  | 'Iniciado'
  | 'EnProgreso'
  | 'Pausado'
  | 'EsperandoRevisionHumana'
  | 'Completado'
  | 'Fallido'
  | 'Cancelado'
  | 'Pendiente'

export interface CasoListItemDto {
  id: string
  titulo: string
  estado: CasoEstado
  estadoNegocio: EstadoNegocioDto | null
  tipoCaso: string | null
  flujoId: string
  flujoVersionId: string
  createdAt: string
  updatedAt: string
  completedAt: string | null
  /** While a robot runs a step of this case: how far along it says it is (0–100). */
  progresoPorcentaje?: number | null
  /** A robot is running it and its screen can be watched right now. */
  enVivo?: boolean
}

export interface EjecucionPasoDto {
  id: string
  ejecucionId: string
  flujoPasoDefId: string
  tipoPaso: string
  numeroIntento: number
  estado: string
  trabajoId: string | null
  errorMensaje: string | null
  startedAt: string | null
  finishedAt: string | null
  /** Only for RPA steps, the executions robots take from a queue: higher goes first among the ones waiting for the same
   * service. 0 is the ordinary one. Null for any other kind of step. */
  prioridad: number | null
  /** The execution is waiting for a robot to take it: the only time its priority can change. */
  enCola: boolean
  /** For an RPA step, the service whose robots run it. */
  servicioNombre?: string | null
  /** For an RPA step a robot has taken, the machine that robot runs on. */
  equipoNombre?: string | null
  /** While a robot runs the step: how far along it says it is, what it says it is doing, and where its screen can be watched. */
  progresoPorcentaje?: number | null
  progresoMensaje?: string | null
  vistaEnDirectoUrl?: string | null
}

export interface EjecucionDto {
  id: string
  casoId: string
  flujoVersionId: string
  estado: string
  pasoActualId: string | null
  startedAt: string
  finishedAt: string | null
  pasos: EjecucionPasoDto[]
}

export interface CasoEventoDto {
  id: string
  actorUserId: string | null
  ejecucionId: string | null
  accion: string
  detalleJson: string | null
  occurredAt: string
}

export interface CasoDetailDto extends CasoListItemDto {
  datosJson: string | null
  ejecucionActual: EjecucionDto | null
  eventosRecientes: CasoEventoDto[]
}

export interface EjecucionResumenDto {
  id: string
  casoId: string
  estado: string
  pasosCount: number
  startedAt: string
  finishedAt: string | null
}

export type CasoTimelineItemTipo = 'EstadoCambiado' | 'Documento' | 'Evidencia'

export interface CasoTimelineItemDto {
  /** Step this item came from (evidencias, and documents produced by a step); null otherwise. */
  pasoNombre: string | null
  /** File details when the item has a stored file (documents and evidencias with an attachment). */
  nombreArchivo: string | null
  contentType: string | null
  tamanoBytes: number | null
  id: string
  tipo: CasoTimelineItemTipo
  occurredAt: string
  titulo: string
  estadoCodigo: string | null
  documentoId: string | null
  evidenciaTipo: EvidenciaTipo | null
  contenidoJson: string | null
}

export interface DocumentoClasificacionDto {
  id: string
  documentoId: string
  tipoDocumentoId: string
  tipoDocumentoNombre: string
  paginaDesde: number
  paginaHasta: number
  createdAt: string
}

export interface DocumentoDto {
  id: string
  casoId: string
  ejecucionPasoId: string | null
  nombre: string
  contentType: string
  tamanoBytes: number
  hash: string | null
  createdAt: string
  clasificaciones: DocumentoClasificacionDto[]
  /** The step that produced the file, when a robot generated it rather than a person uploading it. */
  pasoNombre?: string | null
}

export interface TipoDocumentoDto {
  id: string
  nombre: string
  descripcion: string | null
  activo: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateTipoDocumentoInput {
  nombre: string
  descripcion?: string | null
}

export interface UpdateTipoDocumentoInput {
  nombre?: string
  descripcion?: string | null
  activo?: boolean
}

export type EvidenciaTipo = 'Screenshot' | 'Video' | 'ArchivoGenerado' | 'DatosExtraidos' | 'Otro'

export interface EvidenciaDto {
  id: string
  ejecucionPasoId: string
  casoId: string
  tipo: EvidenciaTipo
  titulo: string
  contenidoJson: string | null
  documentoId: string | null
  createdAt: string
}

export interface FlujoPasoDefDto {
  id: string
  flujoVersionId: string
  orden: number
  nombre: string
  tipoPaso: string
  agenteDefinicionId: string | null
  servicioId: string | null
  configuracionJson: string | null
}

export interface FlujoVersionDetailDto {
  id: string
  flujoId: string
  numeroVersion: number
  estado: string
  notas: string | null
  createdAt: string
  publishedAt: string | null
  pasos: FlujoPasoDefDto[]
}

export interface StartCasoRequest {
  flujoId: string
  titulo: string
  datosJson?: string | null
  estadoNegocioInicialId?: string | null
  tipoCasoId?: string | null
  pasoInicialId?: string | null
}

export interface ListCasosFilters {
  estado?: string
  tipoCasoId?: string
  estadoNegocioCodigo?: string
  finalizado?: boolean
  flujoId?: string
  /** Several processes, separated by commas. */
  flujoIds?: string
  search?: string
  desde?: string
  hasta?: string
  /** List the Casos the dashboard counts: the ones still moving plus the finished ones inside this window
   * (`finalizados`/`completadoDesde`/`completadoHasta`, the same meaning as in the summary). */
  ventana?: boolean
  finalizados?: 'todos'
  completadoDesde?: string
  completadoHasta?: string
  /** Only the Casos that can still be acted on (waiting, running, paused or waiting for a person). */
  activos?: boolean
}

export interface ResumenFilters {
  desde?: string
  hasta?: string
  finalizados?: 'todos'
  flujoId?: string
}

const buildQuery = (params: Record<string, string | undefined>) => {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value) search.set(key, value)
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}

export const listFlujosAsignados = () => apiFetch<FlujoAsignadoDto[]>('/flujos/asignados')

export const getResumen = (filters: ResumenFilters = {}) =>
  apiFetch<FlujoResumenDto[]>(`/casos/resumen${buildQuery({ ...filters })}`)

export const listCasos = (filters: ListCasosFilters & { page?: number; pageSize?: number } = {}) =>
  apiFetch<PagedResult<CasoListItemDto>>(
    `/casos${buildQuery({
      ...filters,
      finalizado: filters.finalizado === undefined ? undefined : String(filters.finalizado),
      ventana: filters.ventana ? 'true' : undefined,
      activos: filters.activos ? 'true' : undefined,
      page: filters.page?.toString(),
      pageSize: filters.pageSize?.toString(),
    })}`,
  )

export const getCaso = (id: string) => apiFetch<CasoDetailDto>(`/casos/${id}`)

/** Cancels an execution (an RPA step waiting for a robot or being run) and, with it, its Caso. */
export const cancelarEjecucion = (casoId: string, ejecucionPasoId: string) =>
  apiFetch<void>(`/casos/${casoId}/pasos/${ejecucionPasoId}/cancelar`, { method: 'POST' })

/** The most Casos one bulk request takes (the server's limit); a longer selection goes in several. */
export const MAXIMO_POR_PETICION = 500

/**
 * Runs a bulk action (the one the URL names: "cancelar"…) on a selection of Casos. `parametros` is whatever that action
 * reads besides the selection; most read nothing. The answer says how many it was applied to and why each of the others was not.
 */
export const ejecutarAccionMasiva = (accion: string, ids: readonly string[], parametros?: unknown) =>
  apiFetch<ResultadoMasivo>(`/casos/acciones/${accion}`, { method: 'POST', body: JSON.stringify({ ids, parametros }) })

export const cambiarPrioridad = (casoId: string, ejecucionPasoId: string, prioridad: number) =>
  apiFetch<EjecucionPasoDto>(`/casos/${casoId}/pasos/${ejecucionPasoId}/prioridad`, {
    method: 'PATCH',
    body: JSON.stringify({ prioridad }),
  })

export const getFlujoVersion = (flujoId: string, versionId: string) =>
  apiFetch<FlujoVersionDetailDto>(`/flujos/${flujoId}/versiones/${versionId}`)

export const startCaso = (request: StartCasoRequest) =>
  apiFetch<CasoListItemDto>('/casos', {
    method: 'POST',
    body: JSON.stringify({
      flujoId: request.flujoId,
      flujoVersionId: null,
      titulo: request.titulo,
      datosJson: request.datosJson ?? null,
      estadoNegocioInicialId: request.estadoNegocioInicialId ?? null,
      tipoCasoId: request.tipoCasoId ?? null,
      pasoInicialId: request.pasoInicialId ?? null,
    }),
  })

export const getEvidencias = (casoId: string) => apiFetch<EvidenciaDto[]>(`/casos/${casoId}/evidencias`)

export const getTimeline = (casoId: string) => apiFetch<CasoTimelineItemDto[]>(`/casos/${casoId}/timeline`)

export const listEjecuciones = (casoId: string) => apiFetch<EjecucionResumenDto[]>(`/casos/${casoId}/ejecuciones`)

export const getEjecucion = (casoId: string, ejecucionId: string) =>
  apiFetch<EjecucionDto>(`/casos/${casoId}/ejecuciones/${ejecucionId}`)

export const documentoContenidoPath = (documentoId: string) => `/documentos/${documentoId}/contenido`

export const listDocumentos = (casoId: string) => apiFetch<DocumentoDto[]>(`/casos/${casoId}/documentos`)

export const uploadDocumento = (casoId: string, file: File) => {
  const formData = new FormData()
  formData.append('file', file)
  return apiFetch<DocumentoDto>(`/casos/${casoId}/documentos`, { method: 'POST', body: formData })
}

export const listTiposDocumento = (filters: Record<string, string> = {}) =>
  apiFetch<PagedResult<TipoDocumentoDto>>(`/tipos-documento${buildQuery({ searchTerm: filters.search, page: filters.page, pageSize: filters.pageSize ?? '100' })}`)

export const createTipoDocumento = (input: CreateTipoDocumentoInput) =>
  apiFetch<TipoDocumentoDto>('/tipos-documento', { method: 'POST', body: JSON.stringify(input) })

export const updateTipoDocumento = (id: string, input: UpdateTipoDocumentoInput) =>
  apiFetch<TipoDocumentoDto>(`/tipos-documento/${id}`, { method: 'PATCH', body: JSON.stringify(input) })

export const deleteTipoDocumento = (id: string) => apiFetch<void>(`/tipos-documento/${id}`, { method: 'DELETE' })

export const createClasificacion = (documentoId: string, input: { tipoDocumentoId: string; paginaDesde: number; paginaHasta: number }) =>
  apiFetch<DocumentoClasificacionDto>(`/documentos/${documentoId}/clasificaciones`, { method: 'POST', body: JSON.stringify(input) })

export const updateClasificacion = (
  documentoId: string,
  id: string,
  input: Partial<{ tipoDocumentoId: string; paginaDesde: number; paginaHasta: number }>,
) => apiFetch<DocumentoClasificacionDto>(`/documentos/${documentoId}/clasificaciones/${id}`, { method: 'PATCH', body: JSON.stringify(input) })

export const deleteClasificacion = (documentoId: string, id: string) =>
  apiFetch<void>(`/documentos/${documentoId}/clasificaciones/${id}`, { method: 'DELETE' })

export const pausarCaso = (id: string) => apiFetch<void>(`/casos/${id}/pausar`, { method: 'POST' })

export const reanudarCaso = (id: string) => apiFetch<void>(`/casos/${id}/reanudar`, { method: 'POST' })

export const cancelarCaso = (id: string) => apiFetch<void>(`/casos/${id}/cancelar`, { method: 'POST' })

export const reprocesarPaso = (casoId: string, ejecucionPasoId: string) =>
  apiFetch<void>(`/casos/${casoId}/pasos/${ejecucionPasoId}/reprocesar`, { method: 'POST' })

/** A creator as the person creating a case sees it. */
export interface CreadorDisponibleDto {
  id: string
  nombre: string
  descripcion: string | null
  flujoId: string
  flujoNombre: string
  tipoCasoId: string | null
  tipoCasoNombre: string | null
  /** The form of the data of the creator's case type, or null when the data is free-form JSON. */
  esquemaDatosJson: string | null
  /** What the title would be if the case were created now: the placeholder of the title field. */
  tituloEjemplo: string
}

export const listCreadoresDisponibles = () => apiFetch<CreadorDisponibleDto[]>('/creadores-de-caso')

/** `titulo` empty or null: the creator writes it. */
export const crearCasoDesdeCreador = (creadorId: string, request: { titulo: string | null; datosJson: string | null }) =>
  apiFetch<CasoListItemDto>(`/creadores-de-caso/${creadorId}/casos`, { method: 'POST', body: JSON.stringify(request) })

/**
 * The documents of these Casos in one zip (a folder per case). `blob` is null when none of them had any. At most
 * `MAXIMO_POR_PETICION` cases; `resumen` says how many documents went in and which cases were left out, and why.
 */
export async function descargarDocumentosDeCasos(ids: readonly string[]) {
  const { blob, headers } = await apiFetchArchivo('/casos/documentos/zip', { method: 'POST', body: JSON.stringify({ ids }) })
  return {
    blob,
    nombre: nombreDeContentDisposition(headers.get('Content-Disposition')),
    resumen: leerResumenDeZip(headers.get(CABECERA_DE_RESUMEN)),
  }
}
