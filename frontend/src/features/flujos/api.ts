import { apiFetch } from '../../lib/apiClient'
import type { PagedResult } from '../../lib/types'

export interface FlujoDto {
  id: string
  nombre: string
  descripcion: string | null
  versionActivaId: string | null
  numeroVersionActiva: number | null
  activo: boolean
  createdAt: string
  updatedAt: string
}

export interface FlujoEstadoDefDto {
  id: string
  flujoId: string
  codigo: string
  display: string
  orden: number
  esFinal: boolean
  activo: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateFlujoEstadoRequest {
  codigo: string
  display: string
  orden: number
  esFinal: boolean
}

export interface UpdateFlujoEstadoRequest {
  display?: string
  orden?: number
  esFinal?: boolean
  activo?: boolean
}

export interface FlujoTipoCasoDefDto {
  id: string
  flujoId: string
  nombre: string
  orden: number
  activo: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateFlujoTipoCasoRequest {
  nombre: string
  orden: number
}

export interface UpdateFlujoTipoCasoRequest {
  nombre?: string
  orden?: number
  activo?: boolean
}

export interface CreateFlujoRequest {
  nombre: string
  descripcion?: string | null
}

export interface UpdateFlujoRequest {
  nombre?: string
  descripcion?: string | null
  activo?: boolean
}

const buildQuery = (params: Record<string, string | undefined>) => {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value) search.set(key, value)
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}

export const listFlujos = (filters: Record<string, string> = {}) =>
  apiFetch<PagedResult<FlujoDto>>(`/flujos${buildQuery({ searchTerm: filters.search, pageSize: '100' })}`)

export const getFlujo = (id: string) => apiFetch<FlujoDto>(`/flujos/${id}`)

export const createFlujo = (request: CreateFlujoRequest) =>
  apiFetch<FlujoDto>('/flujos', { method: 'POST', body: JSON.stringify(request) })

export const updateFlujo = (id: string, request: UpdateFlujoRequest) =>
  apiFetch<FlujoDto>(`/flujos/${id}`, { method: 'PATCH', body: JSON.stringify(request) })

export const deleteFlujo = (id: string) => apiFetch<void>(`/flujos/${id}`, { method: 'DELETE' })

export const listFlujoEstados = (flujoId: string) => apiFetch<FlujoEstadoDefDto[]>(`/flujos/${flujoId}/estados`)

export const createFlujoEstado = (flujoId: string, request: CreateFlujoEstadoRequest) =>
  apiFetch<FlujoEstadoDefDto>(`/flujos/${flujoId}/estados`, {
    method: 'POST',
    body: JSON.stringify(request),
  })

export const updateFlujoEstado = (flujoId: string, id: string, request: UpdateFlujoEstadoRequest) =>
  apiFetch<FlujoEstadoDefDto>(`/flujos/${flujoId}/estados/${id}`, {
    method: 'PATCH',
    body: JSON.stringify(request),
  })

export const listFlujoTiposCaso = (flujoId: string) => apiFetch<FlujoTipoCasoDefDto[]>(`/flujos/${flujoId}/tipos-caso`)

export const createFlujoTipoCaso = (flujoId: string, request: CreateFlujoTipoCasoRequest) =>
  apiFetch<FlujoTipoCasoDefDto>(`/flujos/${flujoId}/tipos-caso`, {
    method: 'POST',
    body: JSON.stringify(request),
  })

export const updateFlujoTipoCaso = (flujoId: string, id: string, request: UpdateFlujoTipoCasoRequest) =>
  apiFetch<FlujoTipoCasoDefDto>(`/flujos/${flujoId}/tipos-caso/${id}`, {
    method: 'PATCH',
    body: JSON.stringify(request),
  })
