import { apiFetch } from '../../lib/apiClient'
import type { PagedResult } from '../../lib/types'

export interface EquipoDto {
  id: string
  nombre: string
  descripcion: string | null
  activo: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateEquipoInput {
  nombre: string
  descripcion?: string | null
}

export interface UpdateEquipoInput {
  nombre?: string
  descripcion?: string | null
  activo?: boolean
}

export interface ServicioDto {
  id: string
  nombre: string
  descripcion: string | null
  activo: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateServicioInput {
  nombre: string
  descripcion?: string | null
}

export interface UpdateServicioInput {
  nombre?: string
  descripcion?: string | null
  activo?: boolean
}

export interface DespliegueDto {
  id: string
  equipoId: string
  equipoNombre: string
  servicioId: string
  servicioNombre: string
  flujoId: string
  encendido: boolean
  apiKeyPrefix: string
  createdAt: string
  updatedAt: string
  lastUsedAt: string | null
}

export interface DespliegueConApiKeyDto {
  despliegue: DespliegueDto
  apiKey: string
}

export interface CreateDespliegueInput {
  equipoId: string
  servicioId: string
  flujoId: string
}

export interface UpdateDespliegueInput {
  encendido?: boolean
}

const buildQuery = (params: Record<string, string | undefined>) => {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value) search.set(key, value)
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}

export const listEquipos = (filters: Record<string, string> = {}) =>
  apiFetch<PagedResult<EquipoDto>>(`/equipos${buildQuery({ searchTerm: filters.search, pageSize: '100' })}`)

export const createEquipo = (input: CreateEquipoInput) =>
  apiFetch<EquipoDto>('/equipos', { method: 'POST', body: JSON.stringify(input) })

export const updateEquipo = (id: string, input: UpdateEquipoInput) =>
  apiFetch<EquipoDto>(`/equipos/${id}`, { method: 'PATCH', body: JSON.stringify(input) })

export const deleteEquipo = (id: string) => apiFetch<void>(`/equipos/${id}`, { method: 'DELETE' })

export const listServicios = (filters: Record<string, string> = {}) =>
  apiFetch<PagedResult<ServicioDto>>(`/servicios${buildQuery({ searchTerm: filters.search, pageSize: '100' })}`)

export const createServicio = (input: CreateServicioInput) =>
  apiFetch<ServicioDto>('/servicios', { method: 'POST', body: JSON.stringify(input) })

export const updateServicio = (id: string, input: UpdateServicioInput) =>
  apiFetch<ServicioDto>(`/servicios/${id}`, { method: 'PATCH', body: JSON.stringify(input) })

export const deleteServicio = (id: string) => apiFetch<void>(`/servicios/${id}`, { method: 'DELETE' })

export const listDespliegues = (page = 1, pageSize = 100) =>
  apiFetch<PagedResult<DespliegueDto>>(`/despliegues${buildQuery({ page: String(page), pageSize: String(pageSize) })}`)

export const createDespliegue = (input: CreateDespliegueInput) =>
  apiFetch<DespliegueConApiKeyDto>('/despliegues', { method: 'POST', body: JSON.stringify(input) })

export const updateDespliegue = (id: string, input: UpdateDespliegueInput) =>
  apiFetch<DespliegueDto>(`/despliegues/${id}`, { method: 'PATCH', body: JSON.stringify(input) })

export const regenerarClaveDespliegue = (id: string) =>
  apiFetch<DespliegueConApiKeyDto>(`/despliegues/${id}/regenerar-clave`, { method: 'POST' })

export const deleteDespliegue = (id: string) => apiFetch<void>(`/despliegues/${id}`, { method: 'DELETE' })
