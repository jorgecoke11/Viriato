import { Check, type LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

export interface OpcionEnTarjeta {
  id: string
  titulo: string
  descripcion?: string | null
  /** Small facts under the description (the process it belongs to, its type…). */
  detalle?: ReactNode
  icono?: LucideIcon
}

/**
 * Pick one of a few options laid out as cards, when a plain dropdown would hide what each one is. It is a radio group: arrow keys
 * move, the chosen card says so with a tick and a border, not only with a colour. Nothing here knows what the options are.
 */
export function TarjetasDeOpciones({
  opciones,
  valor,
  alCambiar,
  etiqueta,
}: {
  opciones: readonly OpcionEnTarjeta[]
  valor: string | null
  alCambiar: (id: string) => void
  /** What a screen reader hears for the group. */
  etiqueta: string
}) {
  return (
    <div role="radiogroup" aria-label={etiqueta} className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
      {opciones.map((opcion) => {
        const elegida = opcion.id === valor
        const Icono = opcion.icono
        return (
          <button
            key={opcion.id}
            type="button"
            role="radio"
            aria-checked={elegida}
            onClick={() => alCambiar(opcion.id)}
            className={`relative flex flex-col gap-1.5 rounded-xl border p-4 text-left shadow-card transition-colors ${
              elegida ? 'border-indigo-500 bg-indigo-50 ring-1 ring-indigo-500' : 'border-gray-200 bg-surface hover:border-indigo-300 hover:bg-gray-50'
            }`}
          >
            <span className="flex items-start gap-3">
              {Icono && (
                <span className={`mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${elegida ? 'bg-indigo-100 text-indigo-600' : 'bg-gray-100 text-gray-500'}`}>
                  <Icono size={18} aria-hidden="true" />
                </span>
              )}
              <span className="flex min-w-0 flex-1 flex-col">
                <span className="font-medium text-gray-900">{opcion.titulo}</span>
                {opcion.descripcion && <span className="text-sm text-gray-500">{opcion.descripcion}</span>}
              </span>
              <span
                aria-hidden="true"
                className={`flex h-5 w-5 shrink-0 items-center justify-center rounded-full border ${
                  elegida ? 'border-indigo-600 bg-indigo-600 text-white' : 'border-gray-300'
                }`}
              >
                {elegida && <Check size={12} strokeWidth={3} />}
              </span>
            </span>
            {opcion.detalle && <span className="flex flex-wrap items-center gap-1.5 text-xs text-gray-500">{opcion.detalle}</span>}
          </button>
        )
      })}
    </div>
  )
}
