import type { ReactNode } from 'react'
import { estadoDeSeleccion, type Seleccion } from '../../lib/seleccion'
import { Checkbox } from './Checkbox'

export interface ColumnaDeTabla<T> {
  clave: string
  titulo: ReactNode
  celda: (fila: T) => ReactNode
  /** Classes for the cells of this column (width, alignment). */
  className?: string
}

interface Props<T> {
  filas: readonly T[]
  columnas: readonly ColumnaDeTabla<T>[]
  obtenerId: (fila: T) => string
  /** Gives each row a tick box and the header a select-all one. The selection is not the table's: it lives where the
   * caller keeps it, so it can outlive a page change. */
  seleccion?: Seleccion
  /** What to call a row for a screen reader ("Seleccionar «Placas Balay»"). */
  nombreDeFila?: (fila: T) => string
  /** Makes the rows clickable (and shows it with the pointer). */
  alHacerClic?: (fila: T) => void
  /** Dims the rows while a new page is on its way. */
  atenuada?: boolean
  className?: string
  /** Width below which the table scrolls sideways instead of squeezing its columns. */
  anchoMinimo?: string
}

const cabecera = 'px-4 py-2.5 text-left text-xs font-medium tracking-wide whitespace-nowrap text-gray-500 uppercase'

/**
 * A table for any list of rows: columns given as data, an optional selection column, an optional click on the row. It
 * knows nothing about what it lists — loading, empty and error states are the caller's, around it.
 */
export function DataTable<T>({
  filas,
  columnas,
  obtenerId,
  seleccion,
  nombreDeFila,
  alHacerClic,
  atenuada = false,
  className = '',
  anchoMinimo = '40rem',
}: Props<T>) {
  const ids = filas.map(obtenerId)
  const estado = seleccion ? estadoDeSeleccion(seleccion.ids, ids) : 'ninguno'

  return (
    <div className={`overflow-x-auto transition-opacity ${atenuada ? 'opacity-60' : ''} ${className}`}>
      <table className="w-full border-collapse text-sm" style={{ minWidth: anchoMinimo }}>
        <thead className="border-b border-gray-200 bg-gray-50/70">
          <tr>
            {seleccion && (
              <th scope="col" className="w-10 py-2.5 pr-0 pl-4">
                <Checkbox
                  label={estado === 'todos' ? 'Quitar la selección de esta página' : 'Seleccionar toda esta página'}
                  checked={estado === 'todos'}
                  indeterminate={estado === 'algunos'}
                  onChange={() => seleccion.marcar(ids, estado !== 'todos')}
                />
              </th>
            )}
            {columnas.map((columna) => (
              <th key={columna.clave} scope="col" className={cabecera}>
                {columna.titulo}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {filas.map((fila) => {
            const id = obtenerId(fila)
            const marcada = seleccion?.ids.has(id) ?? false
            return (
              <tr
                key={id}
                className={`align-top ${alHacerClic ? 'cursor-pointer' : ''} ${marcada ? 'bg-indigo-50/60' : 'hover:bg-gray-50'}`}
                onClick={alHacerClic ? () => alHacerClic(fila) : undefined}
              >
                {seleccion && (
                  <td className="w-10 py-3 pr-0 pl-4" onClick={(e) => e.stopPropagation()}>
                    <Checkbox
                      label={nombreDeFila ? `Seleccionar «${nombreDeFila(fila)}»` : 'Seleccionar fila'}
                      checked={marcada}
                      onChange={() => seleccion.alternar(id)}
                    />
                  </td>
                )}
                {columnas.map((columna) => (
                  <td key={columna.clave} className={`px-4 py-3 ${columna.className ?? ''}`}>
                    {columna.celda(fila)}
                  </td>
                ))}
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
