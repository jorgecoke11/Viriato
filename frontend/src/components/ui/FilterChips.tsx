import type { LucideIcon } from 'lucide-react'

export interface OpcionDeChip<T extends string> {
  valor: T
  etiqueta: string
  icono?: LucideIcon
  /** How many things the choice leaves; an option with 0 is shown disabled (except the selected one). */
  cantidad?: number
}

/**
 * A row of pills to narrow a list by one choice ("Todo", "Capturas", "Datos"…), each with how many it leaves. One is always
 * selected. It is the light alternative to a dropdown when there are a handful of choices and the counts are worth seeing.
 */
export function FilterChips<T extends string>({
  opciones,
  valor,
  alCambiar,
  'aria-label': ariaLabel,
}: {
  opciones: readonly OpcionDeChip<T>[]
  valor: T
  alCambiar: (valor: T) => void
  'aria-label': string
}) {
  return (
    <div role="group" aria-label={ariaLabel} className="flex flex-wrap items-center gap-1.5">
      {opciones.map(({ valor: v, etiqueta, icono: Icono, cantidad }) => {
        const activa = v === valor
        const vacia = cantidad === 0 && !activa
        return (
          <button
            key={v}
            type="button"
            aria-pressed={activa}
            disabled={vacia}
            className={`inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs font-medium transition-colors ${
              activa
                ? 'border-indigo-600 bg-indigo-600 text-white'
                : 'border-gray-200 bg-surface text-gray-700 hover:border-gray-300 hover:bg-gray-50 disabled:cursor-default disabled:text-gray-400 disabled:hover:border-gray-200 disabled:hover:bg-surface'
            }`}
            onClick={() => alCambiar(v)}
          >
            {Icono && <Icono size={13} aria-hidden="true" />}
            {etiqueta}
            {cantidad !== undefined && <span className={`num font-mono ${activa ? 'text-white/80' : 'text-gray-500'}`}>{cantidad}</span>}
          </button>
        )
      })}
    </div>
  )
}
