import { apiFetch } from '../../lib/apiClient'
import type { UserDto } from '../auth/types'

export interface UpdateProfileInput {
  displayName?: string
  baseCurrency?: string
  timeZone?: string
  locale?: string
}

export const updateProfile = (input: UpdateProfileInput) =>
  apiFetch<UserDto>('/users/me', { method: 'PATCH', body: JSON.stringify(input) })

export const changePassword = (currentPassword: string, newPassword: string) =>
  apiFetch<void>('/users/me/password', {
    method: 'POST',
    body: JSON.stringify({ currentPassword, newPassword }),
  })
