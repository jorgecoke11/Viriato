import { apiFetch } from '../../lib/apiClient'
import type { PagedResult } from '../../lib/types'

export interface FlujoDto {
  id: string
  nombre: string
  descripcion: string | null
  versionActivaId: string | null
  numeroVersionActiva: number | null
  activo: boolean
  storageConfigId: string | null
  storageConfigNombre: string | null
  createdAt: string
  updatedAt: string
}

export type StorageProviderType = 'Local' | 'S3Compatible'

export interface StorageConfigDto {
  id: string
  nombre: string
  proveedor: StorageProviderType
  endpoint: string | null
  region: string | null
  bucketName: string | null
  hasCredentials: boolean
  usePathStyle: boolean
  useSsl: boolean
  localPath: string | null
  activo: boolean
  createdAt: string
  updatedAt: string
}

export interface CreateStorageConfigRequest {
  nombre: string
  proveedor: StorageProviderType
  endpoint?: string | null
  region?: string | null
  bucketName?: string | null
  accessKey?: string | null
  secretKey?: string | null
  usePathStyle: boolean
  useSsl: boolean
  localPath?: string | null
}

export interface UpdateStorageConfigRequest {
  nombre?: string
  endpoint?: string | null
  region?: string | null
  bucketName?: string | null
  accessKey?: string | null
  secretKey?: string | null
  usePathStyle?: boolean
  useSsl?: boolean
  localPath?: string | null
  activo?: boolean
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

export type TipoPaso = 'Rpa' | 'Agente' | 'Api' | 'Interno' | 'Decision' | 'Espera' | 'RevisionHumana'

export type FlujoVersionEstado = 'Borrador' | 'Publicada' | 'Archivada'

export interface FlujoVersionDto {
  id: string
  flujoId: string
  numeroVersion: number
  estado: FlujoVersionEstado
  notas: string | null
  createdAt: string
  publishedAt: string | null
}

export interface FlujoPasoDefDto {
  id: string
  flujoVersionId: string
  orden: number
  nombre: string
  tipoPaso: TipoPaso
  agenteDefinicionId: string | null
  servicioId: string | null
  configuracionJson: string | null
}

export interface FlujoVersionDetailDto extends FlujoVersionDto {
  pasos: FlujoPasoDefDto[]
}

export interface CreateFlujoVersionRequest {
  notas?: string | null
}

export interface FlujoPasoDefInput {
  orden: number
  nombre: string
  tipoPaso: TipoPaso
  agenteDefinicionId?: string | null
  servicioId?: string | null
  configuracionJson?: string | null
}

export interface ReplacePasosRequest {
  pasos: FlujoPasoDefInput[]
}

export interface AgenteDefinicionDto {
  id: string
  nombre: string
  descripcion: string | null
  modelo: string
  activo: boolean
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

export const updateFlujoAlmacenamiento = (flujoId: string, storageConfigId: string | null) =>
  apiFetch<FlujoDto>(`/flujos/${flujoId}/almacenamiento`, {
    method: 'PUT',
    body: JSON.stringify({ storageConfigId }),
  })

export const listStorageConfigs = (filters: Record<string, string> = {}) =>
  apiFetch<PagedResult<StorageConfigDto>>(`/storage-configs${buildQuery({ searchTerm: filters.search, pageSize: '100' })}`)

export const createStorageConfig = (request: CreateStorageConfigRequest) =>
  apiFetch<StorageConfigDto>('/storage-configs', { method: 'POST', body: JSON.stringify(request) })

export const updateStorageConfig = (id: string, request: UpdateStorageConfigRequest) =>
  apiFetch<StorageConfigDto>(`/storage-configs/${id}`, { method: 'PATCH', body: JSON.stringify(request) })

export const deleteStorageConfig = (id: string) => apiFetch<void>(`/storage-configs/${id}`, { method: 'DELETE' })

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

export const listFlujoVersiones = (flujoId: string) => apiFetch<FlujoVersionDto[]>(`/flujos/${flujoId}/versiones`)

export const getFlujoVersion = (flujoId: string, versionId: string) =>
  apiFetch<FlujoVersionDetailDto>(`/flujos/${flujoId}/versiones/${versionId}`)

export const createFlujoVersion = (flujoId: string, request: CreateFlujoVersionRequest) =>
  apiFetch<FlujoVersionDto>(`/flujos/${flujoId}/versiones`, { method: 'POST', body: JSON.stringify(request) })

export const replacePasos = (flujoId: string, versionId: string, request: ReplacePasosRequest) =>
  apiFetch<FlujoVersionDetailDto>(`/flujos/${flujoId}/versiones/${versionId}/pasos`, {
    method: 'PUT',
    body: JSON.stringify(request),
  })

export const publicarFlujoVersion = (flujoId: string, versionId: string) =>
  apiFetch<FlujoVersionDto>(`/flujos/${flujoId}/versiones/${versionId}/publicar`, { method: 'POST' })

export const archivarFlujoVersion = (flujoId: string, versionId: string) =>
  apiFetch<FlujoVersionDto>(`/flujos/${flujoId}/versiones/${versionId}/archivar`, { method: 'POST' })

export const listAgentes = (filters: Record<string, string> = {}) =>
  apiFetch<PagedResult<AgenteDefinicionDto>>(`/agentes${buildQuery({ searchTerm: filters.search, pageSize: '100' })}`)
