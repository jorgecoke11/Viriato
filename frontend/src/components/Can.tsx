import type { ReactNode } from 'react'
import { useAuth } from '../features/auth/useAuth'

// Cosmetic only — hides UI the user can't act on. The server is the only real
// enforcement point; this never substitutes for backend authorization.
export function Can({ permission, children }: { permission: string; children: ReactNode }) {
  const { can } = useAuth()
  return can(permission) ? <>{children}</> : null
}
