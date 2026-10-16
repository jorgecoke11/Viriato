import type { ReactNode } from 'react'
import { NavLink } from 'react-router-dom'

/**
 * One entry of the menu. The page you are on is a tinted pill with bolder text, flat and without a glow: it is told by shape (the fill)
 * and weight, not by colour alone.
 * The tile reads the link's `aria-current` (which the router sets on the active one), so the whole entry changes together.
 */
export function EnlaceDeMenu({
  to,
  end,
  icono,
  etiqueta,
  onNavigate,
}: {
  to: string
  end?: boolean
  icono: ReactNode
  etiqueta: string
  onNavigate?: () => void
}) {
  return (
    <NavLink
      to={to}
      end={end}
      onClick={onNavigate}
      className={({ isActive }) =>
        `group flex items-center gap-3 rounded-lg px-2 py-1.5 text-[14px] font-medium ${
          isActive
            ? 'bg-indigo-100 font-semibold text-indigo-800'
            : 'text-side-text hover:bg-side-hover hover:text-side-strong'
        }`
      }
    >
      <span
        aria-hidden="true"
        className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg group-aria-[current=page]:text-indigo-700"
      >
        {icono}
      </span>
      <span className="min-w-0 flex-1 truncate">{etiqueta}</span>
    </NavLink>
  )
}

/** The same entry in the icon rail (no label): the tile is the whole button, filled when it is the current page. */
export function EnlaceDeMenuCompacto({
  to,
  end,
  icono,
  etiqueta,
  onNavigate,
}: {
  to: string
  end?: boolean
  icono: ReactNode
  etiqueta: string
  onNavigate?: () => void
}) {
  return (
    <NavLink
      to={to}
      end={end}
      title={etiqueta}
      aria-label={etiqueta}
      onClick={onNavigate}
      className={({ isActive }) =>
        `flex h-10 w-10 items-center justify-center rounded-lg ${
          isActive
            ? 'bg-indigo-100 text-indigo-800'
            : 'text-side-text hover:bg-side-hover hover:text-side-strong'
        }`
      }
    >
      {icono}
    </NavLink>
  )
}
