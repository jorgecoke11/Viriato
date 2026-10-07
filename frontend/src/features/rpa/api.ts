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
  /** The one process this robot may create Casos in; null when it may not create any. */
  flujoDestinoId: string | null
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
  flujoDestinoId?: string
}

export interface UpdateDespliegueInput {
  encendido?: boolean
  flujoDestinoId?: string
  /** True removes the permission to create Casos. */
  quitarFlujoDestino?: boolean
}

/** Never carries the password: it is write-only from the web, and only reaches a robot. */
export interface CredencialDto {
  id: string
  nombre: string
  descripcion: string | null
  usuario: string | null
  servicioId: string | null
  servicioNombre: string | null
  activo: boolean
  ultimoAccesoAt: string | null
  createdAt: string
  updatedAt: string
}

export interface CreateCredencialInput {
  nombre: string
  descripcion?: string | null
  usuario?: string | null
  password: string
  servicioId?: string | null
}

/** A full replacement of what is editable: null servicioId = any robot, empty password = keep the stored one. */
export interface UpdateCredencialInput {
  descripcion: string | null
  usuario: string | null
  servicioId: string | null
  activo: boolean
  password?: string | null
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

export const listCredenciales = () => apiFetch<PagedResult<CredencialDto>>(`/credenciales${buildQuery({ pageSize: '100' })}`)

export const createCredencial = (input: CreateCredencialInput) =>
  apiFetch<CredencialDto>('/credenciales', { method: 'POST', body: JSON.stringify(input) })

export const updateCredencial = (id: string, input: UpdateCredencialInput) =>
  apiFetch<CredencialDto>(`/credenciales/${id}`, { method: 'PUT', body: JSON.stringify(input) })

export const deleteCredencial = (id: string) => apiFetch<void>(`/credenciales/${id}`, { method: 'DELETE' })
