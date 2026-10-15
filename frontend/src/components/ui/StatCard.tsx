import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ContadorAnimado } from './ContadorAnimado'

type Tone = 'neutral' | 'brand' | 'info' | 'success' | 'warning' | 'danger'

const tones: Record<Tone, string> = {
  neutral: 'bg-gray-100 text-gray-600',
  brand: 'bg-indigo-100 text-indigo-600',
  info: 'bg-blue-100 text-blue-600',
  success: 'bg-green-100 text-green-600',
  warning: 'bg-amber-100 text-amber-600',
  danger: 'bg-red-100 text-red-600',
}

/** One headline figure: an icon tile, the number, what it counts. */
export function StatCard({
  label,
  value,
  hint,
  icon,
  tone = 'neutral',
  to,
}: {
  label: string
  value: ReactNode
  hint?: ReactNode
  icon?: ReactNode
  tone?: Tone
  /** Makes the whole card a link to where these figures can be looked at one by one. */
  to?: string
}) {
  const caja = 'flex items-center gap-4 rounded-xl border border-gray-200 bg-surface p-4 shadow-card'
  // A function and not a component declared here: a component made on every render is a new type each time, which remounts its content
  // (and would restart the counter below every time the page refreshes its data).
  const envolver = (children: ReactNode) =>
    to ? (
      <Link to={to} className={`${caja} transition-colors hover:border-gray-300 hover:bg-gray-50 focus-visible:outline-2 focus-visible:outline-indigo-500`}>
        {children}
      </Link>
    ) : (
      <div className={caja}>{children}</div>
    )

  return envolver(
    <>
      {/* Hidden on a phone: two cards per row leave the text no room otherwise. */}
      {icon && <span className={`hidden h-11 w-11 shrink-0 items-center justify-center rounded-lg sm:flex ${tones[tone]}`}>{icon}</span>}
      <div className="min-w-0">
        <p className="truncate text-xs font-medium tracking-wide text-gray-500 uppercase">{label}</p>
        <p className="num text-2xl leading-8 font-semibold text-gray-900">{typeof value === 'number' ? <ContadorAnimado valor={value} /> : value}</p>
        {hint && <p className="truncate text-xs text-gray-500">{hint}</p>}
      </div>
    </>,
  )
}
