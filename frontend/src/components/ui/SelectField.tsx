import type { SelectHTMLAttributes } from 'react'

export interface OpcionDeSelect {
  valor: string
  etiqueta: string
}

type Props = Omit<SelectHTMLAttributes<HTMLSelectElement>, 'aria-label' | 'value' | 'onChange' | 'children'> & {
  /** What the choice is about, for a screen reader (the first option is usually "any", which names nothing). */
  label: string
  value: string
  onChange: (valor: string) => void
  opciones: readonly OpcionDeSelect[]
}

/** A drop-down with the shared field style and an accessible name, from a plain list of options. */
export function SelectField({ label, value, onChange, opciones, className = '', ...props }: Props) {
  return (
    <label className={`block ${className}`}>
      <span className="sr-only">{label}</span>
      <select className="field" value={value} onChange={(e) => onChange(e.target.value)} {...props}>
        {opciones.map((o) => (
          <option key={o.valor} value={o.valor}>
            {o.etiqueta}
          </option>
        ))}
      </select>
    </label>
  )
}
