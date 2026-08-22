import { createContext, useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { setAccessToken } from '../../lib/tokenStore'
import { SESSION_EXPIRED_EVENT } from '../../lib/apiClient'
import * as authApi from './api'
import type { UserDto } from './types'

export type AuthStatus = 'loading' | 'authenticated' | 'anonymous'

export interface AuthContextValue {
  status: AuthStatus
  user: UserDto | null
  login: (email: string, password: string) => Promise<void>
  register: (email: string, password: string, displayName: string) => Promise<void>
  logout: () => Promise<void>
  can: (permission: string) => boolean
  updateUser: (user: UserDto) => void
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthStatus>('loading')
  const [user, setUser] = useState<UserDto | null>(null)
  const hasBootstrapped = useRef(false)

  // A page reload always starts with no access token in memory, so the only way to know
  // whether the session is still alive is to attempt one silent refresh before rendering routes.
  // Guarded by a ref so React 18 StrictMode's dev-only double-invoke doesn't burn two calls
  // against the /auth/refresh rate limit on every mount.
  useEffect(() => {
    if (hasBootstrapped.current) return
    hasBootstrapped.current = true

    authApi
      .refresh()
      .then((data) => {
        setAccessToken(data.accessToken)
        setUser(data.user)
        setStatus('authenticated')
      })
      .catch(() => {
        setAccessToken(null)
        setUser(null)
        setStatus('anonymous')
      })
  }, [])

  useEffect(() => {
    const handleSessionExpired = () => {
      setAccessToken(null)
      setUser(null)
      setStatus('anonymous')
    }
    window.addEventListener(SESSION_EXPIRED_EVENT, handleSessionExpired)
    return () => window.removeEventListener(SESSION_EXPIRED_EVENT, handleSessionExpired)
  }, [])

  const login = useCallback(async (email: string, password: string) => {
    const data = await authApi.login(email, password)
    setAccessToken(data.accessToken)
    setUser(data.user)
    setStatus('authenticated')
  }, [])

  const register = useCallback(async (email: string, password: string, displayName: string) => {
    const data = await authApi.register(email, password, displayName)
    setAccessToken(data.accessToken)
    setUser(data.user)
    setStatus('authenticated')
  }, [])

  const logout = useCallback(async () => {
    try {
      await authApi.logout()
    } finally {
      setAccessToken(null)
      setUser(null)
      setStatus('anonymous')
    }
  }, [])

  const can = useCallback((permission: string) => user?.permissions.includes(permission) ?? false, [user])

  const updateUser = useCallback((next: UserDto) => setUser(next), [])

  const value = useMemo<AuthContextValue>(
    () => ({ status, user, login, register, logout, can, updateUser }),
    [status, user, login, register, logout, can, updateUser],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
