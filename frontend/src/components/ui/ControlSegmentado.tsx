interface ControlSegmentadoProps<T extends string> {
  opciones: { valor: T; etiqueta: string }[]
  valor: T
  onChange: (valor: T) => void
  /** What the choice is about, for a screen reader ("Cómo escribir los datos"). */
  etiqueta: string
}

/** A small row of mutually exclusive buttons, for switching how something is shown or edited (form / JSON…). */
export function ControlSegmentado<T extends string>({ opciones, valor, onChange, etiqueta }: ControlSegmentadoProps<T>) {
  return (
    <div role="radiogroup" aria-label={etiqueta} className="inline-flex rounded-lg border border-gray-200 p-0.5 text-xs">
      {opciones.map((o) => (
        <button
          key={o.valor}
          type="button"
          role="radio"
          aria-checked={valor === o.valor}
          className={`rounded-md px-2 py-0.5 ${valor === o.valor ? 'bg-indigo-50 font-medium text-indigo-700' : 'text-gray-500 hover:text-gray-800'}`}
          onClick={() => onChange(o.valor)}
        >
          {o.etiqueta}
        </button>
      ))}
    </div>
  )
}
