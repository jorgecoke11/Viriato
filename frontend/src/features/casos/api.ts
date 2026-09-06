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

export const pausarCaso = (id: string) => apiFetch<void>(`/casos/${id}/pausar`, { method: 'POST' })

export const reanudarCaso = (id: string) => apiFetch<void>(`/casos/${id}/reanudar`, { method: 'POST' })

export const cancelarCaso = (id: string) => apiFetch<void>(`/casos/${id}/cancelar`, { method: 'POST' })

export const reintentarPaso = (casoId: string, ejecucionPasoId: string) =>
  apiFetch<void>(`/casos/${casoId}/pasos/${ejecucionPasoId}/reintentar`, { method: 'POST' })
