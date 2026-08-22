import { apiFetch } from '../../lib/apiClient'
import type { AuthResponse } from './types'

export const login = (email: string, password: string) =>
  apiFetch<AuthResponse>('/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  })

export const register = (email: string, password: string, displayName: string) =>
  apiFetch<AuthResponse>('/auth/register', {
    method: 'POST',
    body: JSON.stringify({ email, password, displayName }),
  })

export const refresh = () => apiFetch<AuthResponse>('/auth/refresh', { method: 'POST' })

export const logout = () => apiFetch<void>('/auth/logout', { method: 'POST' })
