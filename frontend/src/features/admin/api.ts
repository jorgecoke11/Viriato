import { apiFetch } from '../../lib/apiClient'
import type { PagedResult } from '../../lib/types'
import type { UserDto } from '../auth/types'
import type { FlujoDto } from '../flujos/api'

export type { FlujoDto }

export interface AsignacionFlujoDto {
  id: string
  flujoId: string
  userId: string
  createdAt: string
}

export interface RoleDto {
  id: string
  name: string
  description: string | null
  isSystem: boolean
  permissions: string[]
  createdAt: string
}

export interface PermissionDto {
  id: string
  name: string
  module: string
  action: string
  description: string | null
}

export interface CreateRoleInput {
  name: string
  description: string
}

export interface UpdateRoleInput {
  name?: string
  description?: string
}

export interface CreateUserInput {
  email: string
  password: string
  displayName: string
}

export interface UpdateUserInput {
  displayName?: string
  isActive?: boolean
}

const buildQuery = (params: Record<string, string | undefined>) => {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value) search.set(key, value)
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}

// The hand-written /users endpoint reads "search"; the generic Crud kit backing /roles and
// /permissions reads "searchTerm" — each adapter below translates the CrudPage's neutral
// `filters.search` into whatever query param its own backend endpoint actually expects.
export const listUsers = (filters: Record<string, string> = {}) =>
  apiFetch<PagedResult<UserDto>>(`/users${buildQuery({ search: filters.search })}`)

export const createUser = (input: CreateUserInput) =>
  apiFetch<UserDto>('/users', { method: 'POST', body: JSON.stringify(input) })

export const updateUser = (id: string, input: UpdateUserInput) =>
  apiFetch<UserDto>(`/users/${id}`, { method: 'PATCH', body: JSON.stringify(input) })

export const deactivateUser = (id: string) => apiFetch<void>(`/users/${id}`, { method: 'DELETE' })

export const listFlujosAsignadosDeUsuario = (userId: string) =>
  apiFetch<FlujoDto[]>(`/flujos/asignados/${userId}`)

export const asignarFlujo = (flujoId: string, userId: string) =>
  apiFetch<AsignacionFlujoDto>(`/flujos/${flujoId}/asignaciones`, { method: 'POST', body: JSON.stringify({ userId }) })

export const desasignarFlujo = (flujoId: string, userId: string) =>
  apiFetch<void>(`/flujos/${flujoId}/asignaciones/${userId}`, { method: 'DELETE' })

export const assignRole = (userId: string, roleId: string) =>
  apiFetch<UserDto>(`/users/${userId}/roles`, {
    method: 'POST',
    body: JSON.stringify({ roleId }),
  })

export const removeRole = (userId: string, roleId: string) =>
  apiFetch<void>(`/users/${userId}/roles/${roleId}`, { method: 'DELETE' })

export const listRoles = (filters: Record<string, string> = {}) =>
  apiFetch<PagedResult<RoleDto>>(`/roles${buildQuery({ searchTerm: filters.search })}`)

export const createRole = (input: CreateRoleInput) =>
  apiFetch<RoleDto>('/roles', { method: 'POST', body: JSON.stringify(input) })

export const updateRole = (id: string, input: UpdateRoleInput) =>
  apiFetch<RoleDto>(`/roles/${id}`, { method: 'PATCH', body: JSON.stringify(input) })

export const deleteRole = (id: string) => apiFetch<void>(`/roles/${id}`, { method: 'DELETE' })

export const updateRolePermissions = (id: string, permissionIds: string[]) =>
  apiFetch<RoleDto>(`/roles/${id}/permissions`, {
    method: 'PUT',
    body: JSON.stringify({ permissionIds }),
  })

export const listPermissions = (filters: Record<string, string> = {}) =>
  apiFetch<PagedResult<PermissionDto>>(`/permissions${buildQuery({ searchTerm: filters.search })}`)
