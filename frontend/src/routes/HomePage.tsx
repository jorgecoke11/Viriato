import { useAuth } from '../features/auth/useAuth'

export function HomePage() {
  const { user } = useAuth()

  return (
    <div>
      <h1 className="text-2xl font-semibold text-gray-900">Bienvenido, {user?.displayName}</h1>
      <p className="mt-2 text-gray-600">{user?.email}</p>
    </div>
  )
}
