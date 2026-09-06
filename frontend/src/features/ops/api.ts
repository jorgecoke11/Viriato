import { apiFetch } from '../../lib/apiClient'
import type { PagedResult } from '../../lib/types'

export interface TrabajoListItemDto {
  id: string
  processCode: string
  status: string
  subjectType: string | null
  subjectKey: string | null
  parentTrabajoId: string | null
  progress: number | null
  summary: string | null
  error: string | null
  startedAt: string
  finishedAt: string | null
}

export interface TrabajoLogDto {
  id: string
  timestamp: string
  level: string
  message: string
}

export interface TrabajoDetailDto extends TrabajoListItemDto {
  data: string | null
  logs: TrabajoLogDto[]
  children: TrabajoListItemDto[]
}

export interface ListTrabajosFilters {
  processCode?: string
  status?: string
  subjectType?: string
  subjectKey?: string
}

const buildQuery = (filters: ListTrabajosFilters) => {
  const params = new URLSearchParams()
  if (filters.processCode) params.set('processCode', filters.processCode)
  if (filters.status) params.set('status', filters.status)
  if (filters.subjectType) params.set('subjectType', filters.subjectType)
  if (filters.subjectKey) params.set('subjectKey', filters.subjectKey)
  const query = params.toString()
  return query ? `?${query}` : ''
}

export const listTrabajos = (filters: ListTrabajosFilters = {}) =>
  apiFetch<PagedResult<TrabajoListItemDto>>(`/trabajos${buildQuery(filters)}`)

export const getTrabajo = (id: string) => apiFetch<TrabajoDetailDto>(`/trabajos/${id}`)
