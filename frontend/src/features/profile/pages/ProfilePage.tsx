import { useState, type FormEvent } from 'react'
import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
import { Input } from '../../../components/ui/Input'
import { ApiError } from '../../../lib/apiClient'
import { useToast } from '../../../lib/toast/useToast'
import { useAuth } from '../../auth/useAuth'
import type { UserDto } from '../../auth/types'
import * as profileApi from '../api'

export function ProfilePage() {
  const { user, updateUser } = useAuth()

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-semibold text-gray-900">Mi perfil</h1>
      {user && <ProfileForm key={user.id} initial={user} onSaved={updateUser} />}
      <PasswordForm />
    </div>
  )
}

function ProfileForm({
  initial,
  onSaved,
}: {
  initial: { displayName: string; baseCurrency: string; timeZone: string; locale: string }
  onSaved: (user: UserDto) => void
}) {
  const { showToast } = useToast()
  const [displayName, setDisplayName] = useState(initial.displayName)
  const [baseCurrency, setBaseCurrency] = useState(initial.baseCurrency)
  const [timeZone, setTimeZone] = useState(initial.timeZone)
  const [locale, setLocale] = useState(initial.locale)
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      const updated = await profileApi.updateProfile({ displayName, baseCurrency, timeZone, locale })
      onSaved(updated)
      showToast('success', 'Cambios guardados.')
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'No se pudieron guardar los cambios.'
      setError(message)
      showToast('error', message)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Card>
      <h2 className="mb-4 text-lg font-medium text-gray-900">Datos personales</h2>
      <form className="grid max-w-md gap-4" onSubmit={handleSubmit}>
        <Input label="Nombre" name="displayName" value={displayName} onChange={(e) => setDisplayName(e.target.value)} required />
        <Input
          label="Moneda base"
          name="baseCurrency"
          value={baseCurrency}
          maxLength={3}
          onChange={(e) => setBaseCurrency(e.target.value.toUpperCase())}
          required
        />
        <Input label="Zona horaria" name="timeZone" value={timeZone} onChange={(e) => setTimeZone(e.target.value)} required />
        <Input label="Idioma" name="locale" value={locale} onChange={(e) => setLocale(e.target.value)} required />
        {error && <p className="text-sm text-red-600">{error}</p>}
        <Button type="submit" disabled={submitting} className="justify-self-start">
          {submitting ? 'Guardando…' : 'Guardar cambios'}
        </Button>
      </form>
    </Card>
  )
}

function PasswordForm() {
  const { showToast } = useToast()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setError(null)
    setSubmitting(true)
    try {
      await profileApi.changePassword(currentPassword, newPassword)
      setCurrentPassword('')
      setNewPassword('')
      showToast('success', 'Contraseña actualizada. Tus otras sesiones se han cerrado.')
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'No se pudo cambiar la contraseña.'
      setError(message)
      showToast('error', message)
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Card>
      <h2 className="mb-4 text-lg font-medium text-gray-900">Cambiar contraseña</h2>
      <form className="grid max-w-md gap-4" onSubmit={handleSubmit}>
        <Input
          label="Contraseña actual"
          name="currentPassword"
          type="password"
          autoComplete="current-password"
          value={currentPassword}
          onChange={(e) => setCurrentPassword(e.target.value)}
          required
        />
        <Input
          label="Nueva contraseña"
          name="newPassword"
          type="password"
          autoComplete="new-password"
          minLength={12}
          value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)}
          required
        />
        {error && <p className="text-sm text-red-600">{error}</p>}
        <Button type="submit" disabled={submitting} className="justify-self-start">
          {submitting ? 'Cambiando…' : 'Cambiar contraseña'}
        </Button>
      </form>
    </Card>
  )
}
