import { useState, type FormEvent } from 'react'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { ApiError } from '../../../lib/apiClient'
import { useAuth } from '../useAuth'
import { AuthShell } from './AuthShell'

export function LoginPage() {
  const { login, status } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  const redirectTo = (location.state as { from?: string } | null)?.from ?? '/'

  // Already signed in (a bookmark, the back button): nothing to do here.
  if (status === 'authenticated') return <Navigate to={redirectTo} replace />

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await login(email, password)
      navigate(redirectTo, { replace: true })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No se pudo iniciar sesión.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <AuthShell
      titulo="Iniciar sesión"
      descripcion="Entra con tu cuenta para ver tus procesos."
      pie={
        <>
          ¿No tienes cuenta?{' '}
          <Link to="/registro" className="font-medium text-indigo-600 hover:underline">
            Regístrate
          </Link>
        </>
      }
    >
      <form className="flex flex-col gap-4" onSubmit={handleSubmit}>
        <Input
          label="Email"
          name="email"
          type="email"
          autoComplete="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          required
        />
        <Input
          label="Contraseña"
          name="password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          required
        />
        {error && (
          <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {error}
          </p>
        )}
        <Button type="submit" disabled={submitting} className="mt-1">
          {submitting ? 'Entrando…' : 'Entrar'}
        </Button>
      </form>
    </AuthShell>
  )
}
