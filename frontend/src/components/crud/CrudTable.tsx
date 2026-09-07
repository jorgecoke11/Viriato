import { AnimatePresence, motion } from 'framer-motion'
import { Pencil, Trash2 } from 'lucide-react'
import type { ReactNode } from 'react'
import { fadeVariants } from '../../lib/motion/variants'
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
}) {
  const hasActions = Boolean(onEdit || onDelete || renderRowExtra)

  return (
    <table className="w-full min-w-[640px] text-left text-sm">
      <thead className="border-b border-gray-200 text-gray-500">
        <tr>
          {columns.map((column) => (
            <th key={column.key} className="px-4 py-3 font-medium">
              {column.label}
            </th>
          ))}
          {hasActions && <th className="px-4 py-3 font-medium">Acciones</th>}
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
              className="border-b border-gray-100 last:border-0"
            >
              {columns.map((column) => (
                <td key={column.key} className="px-4 py-3">
                  {column.render(item)}
                </td>
              ))}
              {hasActions && (
                <td className="px-4 py-3">
                  <div className="flex items-center gap-3">
                    {renderRowExtra?.(item)}
                    {onEdit && (canEditRow?.(item) ?? true) && (
                      <button
                        type="button"
                        className="text-gray-500 hover:text-gray-900"
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
                        className="text-gray-500 hover:text-red-600"
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
