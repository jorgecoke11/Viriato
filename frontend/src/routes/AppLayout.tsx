import { Link, Outlet } from 'react-router-dom'
import { Can } from '../components/Can'
import { Button } from '../components/ui/Button'
import { useAuth } from '../features/auth/useAuth'

export function AppLayout() {
  const { status, user, logout } = useAuth()

  return (
    <div className="min-h-screen bg-gray-50">
      {status === 'authenticated' && (
        <header className="border-b border-gray-200 bg-white">
          <div className="mx-auto flex max-w-5xl items-center justify-between px-4 py-3">
            <div className="flex items-center gap-6">
              <Link to="/" className="font-semibold text-gray-900">
                Viariato
              </Link>
              <nav className="flex items-center gap-4 text-sm text-gray-600">
                <Link to="/perfil" className="hover:text-gray-900">
                  Perfil
                </Link>
                <Can permission="users.manage">
                  <Link to="/admin/usuarios" className="hover:text-gray-900">
                    Usuarios
                  </Link>
                </Can>
                <Can permission="roles.manage">
                  <Link to="/admin/roles" className="hover:text-gray-900">
                    Roles
                  </Link>
                  <Link to="/admin/permisos" className="hover:text-gray-900">
                    Permisos
                  </Link>
                </Can>
              </nav>
            </div>
            <div className="flex items-center gap-4">
              <span className="text-sm text-gray-600">{user?.displayName}</span>
              <Button variant="ghost" onClick={() => logout()}>
                Cerrar sesión
              </Button>
            </div>
          </div>
        </header>
      )}
      <main className="mx-auto max-w-5xl px-4 py-6">
        <Outlet />
      </main>
    </div>
  )
}
