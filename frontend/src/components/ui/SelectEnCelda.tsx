import type { SelectHTMLAttributes } from 'react'

interface Opcion<T extends string | number> {
  valor: T
  texto: string
}

interface Props<T extends string | number> extends Omit<SelectHTMLAttributes<HTMLSelectElement>, 'value' | 'onChange'> {
  /** Required: in a table row the column title is not the control's name. */
  'aria-label': string
  valor: T
  opciones: readonly Opcion<T>[]
  alCambiar: (valor: T) => void
}

/** A compact select that fits a table row: changes a value of that row in place. */
export function SelectEnCelda<T extends string | number>({ valor, opciones, alCambiar, className = '', ...props }: Props<T>) {
  const numerico = typeof valor === 'number'
  return (
    <select
      {...props}
      className={`field !min-h-8 !w-auto !px-2 !py-1 ${className}`}
      value={valor}
      onChange={(e) => alCambiar((numerico ? Number(e.target.value) : e.target.value) as T)}
    >
      {opciones.map((o) => (
        <option key={o.valor} value={o.valor}>
          {o.texto}
        </option>
      ))}
    </select>
  )
}
