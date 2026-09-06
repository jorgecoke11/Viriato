import { ChevronsLeft, ChevronsRight, LogOut, Settings, Workflow } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuth } from '../../features/auth/useAuth'
import { VersionBadge } from '../../lib/version/VersionBadge'
import { navLinkClass, NavTree } from './NavTree'
import { navTree } from './navConfig'
import { SidebarSearch } from './SidebarSearch'

export function SidebarContent({
  collapsed = false,
  onToggleCollapsed,
  onNavigate,
}: {
  collapsed?: boolean
  onToggleCollapsed?: () => void
  onNavigate?: () => void
}) {
  const { user, logout } = useAuth()
  const [query, setQuery] = useState('')

  return (
    <div className="flex h-full flex-col">
      <div className={`flex items-center gap-2.5 px-4 py-5 ${collapsed ? 'flex-col gap-3' : 'flex-row'}`}>
        <Link
          to="/"
          onClick={onNavigate}
          aria-label="Viariato"
          className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-gradient-to-br from-blue-600 to-indigo-700 text-white shadow-sm"
        >
          <Workflow size={18} />
        </Link>
        {!collapsed && (
          <Link to="/" onClick={onNavigate} className="min-w-0 flex-1 truncate text-base font-semibold tracking-tight text-gray-900">
            Viariato
          </Link>
        )}
        {onToggleCollapsed && (
          <button
            type="button"
            aria-label={collapsed ? 'Expandir menú' : 'Colapsar menú'}
            title={collapsed ? 'Expandir menú' : 'Colapsar menú'}
            className="shrink-0 rounded-md p-1.5 text-gray-400 hover:bg-blue-50 hover:text-blue-700"
            onClick={onToggleCollapsed}
          >
            {collapsed ? <ChevronsRight size={16} /> : <ChevronsLeft size={16} />}
          </button>
        )}
      </div>

      <SidebarSearch collapsed={collapsed} value={query} onChange={setQuery} onRequestExpand={onToggleCollapsed} />

      <NavTree nodes={navTree} collapsed={collapsed} query={query} onNavigate={onNavigate} />

      <div className={`border-t border-blue-100 px-3 py-3 ${collapsed ? 'flex flex-col items-center gap-2' : ''}`}>
        {collapsed ? (
          <>
            <Link
              to="/perfil"
              title="Configuración"
              aria-label="Configuración"
              onClick={onNavigate}
              className="flex h-10 w-10 items-center justify-center rounded-lg text-gray-500 hover:bg-blue-50 hover:text-blue-700"
            >
              <Settings size={18} />
            </Link>
            <span
              className="flex h-9 w-9 items-center justify-center rounded-full bg-indigo-100 text-xs font-semibold text-indigo-700"
              title={user?.displayName}
            >
              {user?.displayName?.[0]?.toUpperCase() ?? '?'}
            </span>
            <button
              type="button"
              aria-label="Cerrar sesión"
              title="Cerrar sesión"
              className="flex h-10 w-10 items-center justify-center rounded-lg text-gray-400 hover:bg-red-50 hover:text-red-600"
              onClick={() => logout()}
            >
              <LogOut size={16} />
            </button>
          </>
        ) : (
          <>
            <Link to="/perfil" onClick={onNavigate} className={navLinkClass({ isActive: false })}>
              <Settings size={18} />
              Configuración
            </Link>
            <div className="mt-2 flex items-center gap-3 rounded-lg px-3 py-2">
              <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-indigo-100 text-sm font-semibold text-indigo-700">
                {user?.displayName?.[0]?.toUpperCase() ?? '?'}
              </span>
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-gray-900">{user?.displayName}</p>
                <p className="truncate text-xs text-gray-500">{user?.email}</p>
              </div>
              <button
                type="button"
                aria-label="Cerrar sesión"
                title="Cerrar sesión"
                className="rounded-md p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-600"
                onClick={() => logout()}
              >
                <LogOut size={16} />
              </button>
            </div>
            <VersionBadge />
          </>
        )}
      </div>
    </div>
  )
}
