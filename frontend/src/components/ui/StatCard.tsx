import type { ReactNode } from 'react'

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
}: {
  label: string
  value: ReactNode
  hint?: ReactNode
  icon?: ReactNode
  tone?: Tone
}) {
  return (
    <div className="flex items-center gap-4 rounded-xl border border-gray-200 bg-surface p-4 shadow-card">
      {/* Hidden on a phone: two cards per row leave the text no room otherwise. */}
      {icon && <span className={`hidden h-11 w-11 shrink-0 items-center justify-center rounded-lg sm:flex ${tones[tone]}`}>{icon}</span>}
      <div className="min-w-0">
        <p className="truncate text-xs font-medium tracking-wide text-gray-500 uppercase">{label}</p>
        <p className="num font-mono text-2xl leading-8 font-medium text-gray-900">{value}</p>
        {hint && <p className="truncate text-xs text-gray-500">{hint}</p>}
      </div>
    </div>
  )
}
