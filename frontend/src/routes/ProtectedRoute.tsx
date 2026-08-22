import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import { useAuth } from '../features/auth/useAuth'

export function ProtectedRoute({ children }: { children: ReactNode }) {
  const { status } = useAuth()
  const location = useLocation()

  // Never render routes (or redirect) while the session bootstrap is still in flight —
  // otherwise every F5 flashes the login page before the silent refresh resolves.
  if (status === 'loading') {
    return null
  }

  if (status === 'anonymous') {
    return <Navigate to="/login" state={{ from: location.pathname }} replace />
  }

  return <>{children}</>
}
