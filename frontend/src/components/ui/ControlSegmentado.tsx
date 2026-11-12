import { motion } from 'framer-motion'
import { useId } from 'react'
import { duration, ease } from '../../lib/motion/tokens'

interface ControlSegmentadoProps<T extends string> {
  opciones: { valor: T; etiqueta: string }[]
  valor: T
  onChange: (valor: T) => void
  /** What the choice is about, for a screen reader ("Cómo escribir los datos"). */
  etiqueta: string
}

/** A small row of mutually exclusive buttons, for switching how something is shown or edited (form / JSON…). */
export function ControlSegmentado<T extends string>({ opciones, valor, onChange, etiqueta }: ControlSegmentadoProps<T>) {
  // Each control has its own sliding pill: two on the same page must not trade theirs.
  const id = useId()
  return (
    <div role="radiogroup" aria-label={etiqueta} className="inline-flex rounded-lg border border-gray-200 p-0.5 text-xs">
      {opciones.map((o) => {
        const elegida = valor === o.valor
        return (
          <button
            key={o.valor}
            type="button"
            role="radio"
            aria-checked={elegida}
            className={`relative rounded-md px-2.5 py-1 select-none focus-visible:outline-2 focus-visible:outline-indigo-500 ${
              elegida ? 'font-medium text-indigo-700' : 'text-gray-500 hover:text-gray-800'
            }`}
            onClick={() => onChange(o.valor)}
          >
            {elegida && (
              <motion.span
                aria-hidden="true"
                layoutId={`${id}-elegida`}
                className="absolute inset-0 rounded-md bg-indigo-50"
                transition={{ duration: duration.fast, ease: ease.inOut }}
              />
            )}
            <span className="relative">{o.etiqueta}</span>
          </button>
        )
      })}
    </div>
  )
}
