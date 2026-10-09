import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

export type TonoDeNodo = 'indigo' | 'blue' | 'green' | 'amber' | 'purple' | 'red' | 'gray'

// The node on the rail: a tint with a readable icon colour, from the colour scales the theme knows, so it follows dark mode.
const nodos: Record<TonoDeNodo, string> = {
  indigo: 'bg-indigo-100 text-indigo-600',
  blue: 'bg-blue-100 text-blue-600',
  green: 'bg-green-100 text-green-600',
  amber: 'bg-amber-100 text-amber-600',
  purple: 'bg-purple-100 text-purple-600',
  red: 'bg-red-100 text-red-600',
  gray: 'bg-gray-100 text-gray-500',
}

/** A vertical history: groups (a day, a phase) of items hanging from one rail. Nothing in it knows what the items are about. */
export function Timeline({ children, 'aria-label': ariaLabel }: { children: ReactNode; 'aria-label'?: string }) {
  return (
    <ol aria-label={ariaLabel} className="flex flex-col gap-6">
      {children}
    </ol>
  )
}

/** A heading ("Hoy", "Ayer") and the items under it. The rail runs through the items of one group. */
export function TimelineGrupo({ etiqueta, detalle, children }: { etiqueta?: string; detalle?: string; children: ReactNode }) {
  return (
    <li>
      {etiqueta && (
        <div className="mb-3 flex items-baseline gap-2">
          <h3 className="text-sm font-semibold text-gray-900">{etiqueta}</h3>
          {detalle && <span className="text-xs text-gray-500">{detalle}</span>}
          <span aria-hidden="true" className="h-px flex-1 bg-gray-200" />
        </div>
      )}
      <ol className="relative flex flex-col gap-3 before:absolute before:top-4 before:bottom-4 before:left-[15px] before:w-px before:bg-gray-200">
        {children}
      </ol>
    </li>
  )
}

/**
 * One thing that happened: an icon node on the rail and whatever the caller draws beside it. `compacto` is for the slight
 * ones (a status change), which are a line of text instead of a card.
 */
export function TimelineItem({
  icono: Icono,
  tono,
  compacto = false,
  girando = false,
  children,
}: {
  icono: LucideIcon
  tono: TonoDeNodo
  compacto?: boolean
  /** The icon turns: something is happening right now. Skipped when the system asks for reduced motion. */
  girando?: boolean
  children: ReactNode
}) {
  return (
    <li className={`relative ${compacto ? 'pl-11' : 'pl-12'}`}>
      <span
        aria-hidden="true"
        className={`absolute z-10 flex items-center justify-center rounded-full ring-4 ring-surface ${nodos[tono]} ${
          compacto ? 'top-0.5 left-[5px] h-[22px] w-[22px]' : 'top-2 left-0 h-[31px] w-[31px]'
        }`}
      >
        <Icono size={compacto ? 12 : 15} className={girando ? 'motion-safe:animate-spin' : ''} />
      </span>
      {children}
    </li>
  )
}
