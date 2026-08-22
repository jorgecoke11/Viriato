import { apiFetch } from '../../lib/apiClient'
import type { PagedResult } from '../../lib/types'
import type { UserDto } from '../auth/types'

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

const withSearch = (path: string, search: string) => {
  const query = search.trim() ? `?search=${encodeURIComponent(search.trim())}` : ''
  return `${path}${query}`
}

export const listUsers = (search: string) => apiFetch<PagedResult<UserDto>>(withSearch('/users', search))

export const assignRole = (userId: string, roleId: string) =>
  apiFetch<UserDto>(`/users/${userId}/roles`, {
    method: 'POST',
    body: JSON.stringify({ roleId }),
  })

export const removeRole = (userId: string, roleId: string) =>
  apiFetch<void>(`/users/${userId}/roles/${roleId}`, { method: 'DELETE' })

export const listRoles = (search: string) => apiFetch<PagedResult<RoleDto>>(withSearch('/roles', search))

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

export const listPermissions = (search: string) =>
  apiFetch<PagedResult<PermissionDto>>(withSearch('/permissions', search))
