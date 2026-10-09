import type { ReactNode } from 'react'
import { NavLink } from 'react-router-dom'

/**
 * One entry of the menu. The icon sits in its own small tile; the page you are on is a filled brand pill (gradient, white text,
 * a soft glow), so it is the first thing the eye finds — and it is also told by shape (the fill), not by colour alone.
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
        `group flex items-center gap-3 rounded-xl px-2 py-1.5 text-[14px] font-medium ${
          isActive
            ? 'bg-gradient-to-r from-indigo-500 to-indigo-600 text-white shadow-[0_8px_20px_-8px_rgb(99_102_241/0.9)]'
            : 'text-side-text hover:bg-side-hover hover:text-side-strong'
        }`
      }
    >
      <span
        aria-hidden="true"
        className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-white/5 group-hover:bg-white/10 group-aria-[current=page]:bg-white/20 group-aria-[current=page]:text-white"
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
        `flex h-10 w-10 items-center justify-center rounded-xl ${
          isActive
            ? 'bg-gradient-to-br from-indigo-500 to-indigo-600 text-white shadow-[0_8px_20px_-8px_rgb(99_102_241/0.9)]'
            : 'text-side-text hover:bg-side-hover hover:text-side-strong'
        }`
      }
    >
      {icono}
    </NavLink>
  )
}
