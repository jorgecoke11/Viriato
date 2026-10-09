import { ChevronsLeft, ChevronsRight, LogOut, Settings, Workflow } from 'lucide-react'
import { useState } from 'react'
import { Link, NavLink } from 'react-router-dom'
import { useAuth } from '../../features/auth/useAuth'
import { ThemeToggle } from '../../lib/theme/ThemeToggle'
import { VersionBadge } from '../../lib/version/VersionBadge'
import { navLinkClass, NavTree } from './NavTree'
import { navTree } from './navConfig'
import { SidebarSearch } from './SidebarSearch'

function Avatar({ name, size }: { name?: string; size: 'sm' | 'md' }) {
  return (
    <span
      title={name}
      className={`flex shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-indigo-400 to-indigo-600 font-semibold text-white ${
        size === 'sm' ? 'h-9 w-9 text-xs' : 'h-9 w-9 text-sm'
      }`}
    >
      {name?.[0]?.toUpperCase() ?? '?'}
    </span>
  )
}

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
      <div className={`flex items-center gap-3 px-4 pt-5 pb-4 ${collapsed ? 'flex-col gap-3' : 'flex-row'}`}>
        <Link
          to="/"
          onClick={onNavigate}
          aria-label="Viariato"
          className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-indigo-400 to-indigo-600 text-white shadow-[0_4px_14px_-4px_rgb(99_102_241/0.7)]"
        >
          <Workflow size={18} />
        </Link>
        {!collapsed && (
          <Link to="/" onClick={onNavigate} className="min-w-0 flex-1 leading-tight">
            <span className="block truncate text-[17px] font-semibold tracking-tight text-side-strong">Viariato</span>
            <span className="block truncate text-[11px] tracking-wide text-side-text">Procesos y robots</span>
          </Link>
        )}
        {onToggleCollapsed && (
          <button
            type="button"
            aria-label={collapsed ? 'Expandir menú' : 'Colapsar menú'}
            title={collapsed ? 'Expandir menú' : 'Colapsar menú'}
            className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-side-text hover:bg-side-hover hover:text-side-strong"
            onClick={onToggleCollapsed}
          >
            {collapsed ? <ChevronsRight size={16} /> : <ChevronsLeft size={16} />}
          </button>
        )}
      </div>

      <SidebarSearch collapsed={collapsed} value={query} onChange={setQuery} onRequestExpand={onToggleCollapsed} />

      <NavTree nodes={navTree} collapsed={collapsed} query={query} onNavigate={onNavigate} />

      <div className={`border-t border-side-border px-3 py-3 ${collapsed ? 'flex flex-col items-center gap-2' : 'flex flex-col gap-2'}`}>
        {collapsed ? (
          <>
            <NavLink
              to="/perfil"
              title="Configuración"
              aria-label="Configuración"
              onClick={onNavigate}
              className={({ isActive }) =>
                `flex h-10 w-10 items-center justify-center rounded-lg ${
                  isActive ? 'bg-indigo-500/15 text-side-accent' : 'text-side-text hover:bg-side-hover hover:text-side-strong'
                }`
              }
            >
              <Settings size={18} />
            </NavLink>
            <ThemeToggle compact />
            <Avatar name={user?.displayName} size="sm" />
            <button
              type="button"
              aria-label="Cerrar sesión"
              title="Cerrar sesión"
              className="flex h-10 w-10 items-center justify-center rounded-lg text-side-text hover:bg-red-500/15 hover:text-red-400"
              onClick={() => logout()}
            >
              <LogOut size={16} />
            </button>
          </>
        ) : (
          <>
            <NavLink to="/perfil" onClick={onNavigate} className={navLinkClass}>
              <Settings size={18} />
              Configuración
            </NavLink>
            <ThemeToggle />
            <div className="flex items-center gap-3 rounded-lg px-1 py-1">
              <Avatar name={user?.displayName} size="md" />
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-side-strong">{user?.displayName}</p>
                <p className="truncate text-xs text-side-text">{user?.email}</p>
              </div>
              <button
                type="button"
                aria-label="Cerrar sesión"
                title="Cerrar sesión"
                className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-side-text hover:bg-red-500/15 hover:text-red-400"
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
