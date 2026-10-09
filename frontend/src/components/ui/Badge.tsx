import type { HTMLAttributes } from 'react'

export type BadgeTone = 'neutral' | 'brand' | 'info' | 'success' | 'warning' | 'danger' | 'review'

// Each tone is a tint + a readable text colour + a dot. They are the same colour names the rest of the interface
// uses, so they follow the dark theme on their own.
const tones: Record<BadgeTone, { box: string; dot: string }> = {
  neutral: { box: 'bg-gray-100 text-gray-700', dot: 'bg-gray-400' },
  brand: { box: 'bg-indigo-100 text-indigo-700', dot: 'bg-indigo-500' },
  info: { box: 'bg-blue-100 text-blue-700', dot: 'bg-blue-500' },
  success: { box: 'bg-green-100 text-green-700', dot: 'bg-green-500' },
  warning: { box: 'bg-amber-100 text-amber-700', dot: 'bg-amber-500' },
  danger: { box: 'bg-red-100 text-red-700', dot: 'bg-red-500' },
  review: { box: 'bg-purple-100 text-purple-700', dot: 'bg-purple-500' },
}

type Props = HTMLAttributes<HTMLSpanElement> & {
  tone?: BadgeTone
  /** A leading dot, so the status does not rest on colour alone. On by default. */
  dot?: boolean
  /** The dot breathes: something is running right now. Skipped when the OS asks for reduced motion. */
  live?: boolean
}

export function Badge({ tone = 'neutral', dot = true, live = false, className = '', children, ...props }: Props) {
  const { box, dot: dotColor } = tones[tone]
  return (
    <span
      className={`inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2.5 py-0.5 text-xs font-medium ${box} ${className}`}
      {...props}
    >
      {dot && <span aria-hidden="true" className={`h-1.5 w-1.5 shrink-0 rounded-full ${dotColor} ${live ? 'motion-safe:animate-pulse' : ''}`} />}
      {children}
    </span>
  )
}
