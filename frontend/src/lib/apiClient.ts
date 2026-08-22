import { getAccessToken, setAccessToken } from './tokenStore'

const API_BASE = '/api/v1'
const REFRESH_PATH = '/auth/refresh'

export const SESSION_EXPIRED_EVENT = 'auth:session-expired'

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public problem?: unknown,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

// Concurrent 401s must share a single /auth/refresh call: rotating refresh tokens invalidates
// the previous one, so firing several refresh requests in parallel would trip reuse detection
// and log everyone out.
let refreshPromise: Promise<boolean> | null = null

async function rawFetch(path: string, options: RequestInit): Promise<Response> {
  const token = getAccessToken()
  const headers = new Headers(options.headers)
  if (token) headers.set('Authorization', `Bearer ${token}`)
  if (options.body && !headers.has('Content-Type')) headers.set('Content-Type', 'application/json')

  return fetch(`${API_BASE}${path}`, {
    ...options,
    headers,
    credentials: 'include',
  })
}

function refreshAccessToken(): Promise<boolean> {
  if (!refreshPromise) {
    refreshPromise = (async () => {
      try {
        const response = await rawFetch(REFRESH_PATH, { method: 'POST' })
        if (!response.ok) {
          setAccessToken(null)
          return false
        }
        const data = await response.json()
        setAccessToken(data.accessToken)
        return true
      } catch {
        setAccessToken(null)
        return false
      } finally {
        refreshPromise = null
      }
    })()
  }
  return refreshPromise
}

// ProblemDetails carries a human `detail`; ValidationProblemDetails (400s from FluentValidation)
// carries no `detail` at all, only a field -> messages map — flatten that into one readable line.
function extractErrorMessage(problem: unknown, fallback: string): string {
  if (problem && typeof problem === 'object') {
    const p = problem as { detail?: string; errors?: Record<string, string[]> }
    if (p.detail) return p.detail
    if (p.errors) {
      const messages = Object.values(p.errors).flat()
      if (messages.length > 0) return messages.join(' ')
    }
  }
  return fallback
}

export async function apiFetch<T>(path: string, options: RequestInit = {}): Promise<T> {
  let response = await rawFetch(path, options)

  if (response.status === 401 && path !== REFRESH_PATH) {
    const refreshed = await refreshAccessToken()
    if (!refreshed) {
      window.dispatchEvent(new Event(SESSION_EXPIRED_EVENT))
      throw new ApiError('La sesión ha expirado.', 401)
    }
    response = await rawFetch(path, options)
  }

  if (!response.ok) {
    const problem = await response.json().catch(() => null)
    throw new ApiError(extractErrorMessage(problem, response.statusText), response.status, problem)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}
