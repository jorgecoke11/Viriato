import { apiFetch } from '../../lib/apiClient'
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
}

export interface FlujoResumenDto {
  flujoId: string
  flujoNombre: string
  total: number
  porTipo: TipoCasoConteoDto[]
}

export interface EstadoNegocioDto {
  codigo: string
  display: string
}

export type CasoEstado =
  | 'Iniciado'
  | 'EnProgreso'
  | 'Pausado'
  | 'EsperandoRevisionHumana'
  | 'Completado'
  | 'Fallido'
  | 'Cancelado'

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
}

export interface ListCasosFilters {
  estado?: string
  tipoCasoId?: string
  estadoNegocioCodigo?: string
  finalizado?: boolean
  flujoId?: string
  search?: string
  desde?: string
  hasta?: string
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
      page: filters.page?.toString(),
      pageSize: filters.pageSize?.toString(),
    })}`,
  )

export const getCaso = (id: string) => apiFetch<CasoDetailDto>(`/casos/${id}`)

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
  apiFetch<PagedResult<TipoDocumentoDto>>(`/tipos-documento${buildQuery({ searchTerm: filters.search, pageSize: '100' })}`)

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
