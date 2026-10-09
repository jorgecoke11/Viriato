import { AnimatePresence, motion } from 'framer-motion'
import { Pencil, Trash2 } from 'lucide-react'
import type { ReactNode } from 'react'
import { fadeVariants } from '../../lib/motion/variants'
import { estadoDeSeleccion, type Seleccion } from '../../lib/seleccion'
import { Checkbox } from '../ui/Checkbox'
import type { CrudColumn } from './types'

export function CrudTable<T>({
  items,
  columns,
  getId,
  onEdit,
  onDelete,
  renderRowExtra,
  canEditRow,
  canDeleteRow,
  seleccion,
  nombreDeFila,
}: {
  items: T[]
  columns: CrudColumn<T>[]
  getId: (item: T) => string
  onEdit?: (item: T) => void
  onDelete?: (item: T) => void
  renderRowExtra?: (item: T) => ReactNode
  /** Hides the edit/delete action for a specific row (e.g. an admin can't edit their own user). */
  canEditRow?: (item: T) => boolean
  canDeleteRow?: (item: T) => boolean
  /** Gives each row a tick box and the header a select-all one (for the page on screen). */
  seleccion?: Seleccion
  /** What to call a row for a screen reader ("Seleccionar «Placas»"). */
  nombreDeFila?: (item: T) => string
}) {
  const hasActions = Boolean(onEdit || onDelete || renderRowExtra)
  const ids = items.map(getId)
  const estado = seleccion ? estadoDeSeleccion(seleccion.ids, ids) : 'ninguno'

  return (
    <table className="w-full min-w-[640px] text-left text-sm">
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
          {columns.map((column) => (
            <th key={column.key} scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">
              {column.label}
            </th>
          ))}
          {hasActions && (
            <th scope="col" className="px-4 py-2.5 text-xs font-medium tracking-wide text-gray-500 uppercase">
              Acciones
            </th>
          )}
        </tr>
      </thead>
      <tbody>
        {/* Only opacity is animated here (no scale/translate) — table rows don't play well with
            transforms across browsers, but a fade still makes add/remove feel like a reflow
            instead of a jump cut. `layout` handles the position shift as rows above are removed. */}
        <AnimatePresence initial={false}>
          {items.map((item) => (
            <motion.tr
              key={getId(item)}
              layout
              variants={fadeVariants}
              initial="initial"
              animate="animate"
              exit="exit"
              className={`border-b border-gray-100 text-gray-800 last:border-0 ${seleccion?.ids.has(getId(item)) ? 'bg-indigo-50/60' : 'hover:bg-gray-50/70'}`}
            >
              {seleccion && (
                <td className="w-10 py-3 pr-0 pl-4 align-middle">
                  <Checkbox
                    label={nombreDeFila ? `Seleccionar «${nombreDeFila(item)}»` : 'Seleccionar fila'}
                    checked={seleccion.ids.has(getId(item))}
                    onChange={() => seleccion.alternar(getId(item))}
                  />
                </td>
              )}
              {columns.map((column) => (
                <td key={column.key} className="px-4 py-3 align-middle">
                  {column.render(item)}
                </td>
              ))}
              {hasActions && (
                <td className="px-4 py-2 align-middle">
                  <div className="flex items-center gap-1">
                    {renderRowExtra?.(item)}
                    {onEdit && (canEditRow?.(item) ?? true) && (
                      <button
                        type="button"
                        className="flex h-9 w-9 items-center justify-center rounded-lg text-gray-500 hover:bg-gray-100 hover:text-gray-900"
                        onClick={() => onEdit(item)}
                        aria-label="Editar"
                        title="Editar"
                      >
                        <Pencil size={16} />
                      </button>
                    )}
                    {onDelete && (canDeleteRow?.(item) ?? true) && (
                      <button
                        type="button"
                        className="flex h-9 w-9 items-center justify-center rounded-lg text-gray-500 hover:bg-red-50 hover:text-red-600"
                        onClick={() => onDelete(item)}
                        aria-label="Eliminar"
                        title="Eliminar"
                      >
                        <Trash2 size={16} />
                      </button>
                    )}
                  </div>
                </td>
              )}
            </motion.tr>
          ))}
        </AnimatePresence>
      </tbody>
    </table>
  )
}
