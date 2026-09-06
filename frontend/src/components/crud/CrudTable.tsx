import { AnimatePresence, motion } from 'framer-motion'
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
}: {
  items: T[]
  columns: CrudColumn<T>[]
  getId: (item: T) => string
  onEdit?: (item: T) => void
  onDelete?: (item: T) => void
  renderRowExtra?: (item: T) => ReactNode
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
                    {onEdit && (
                      <button type="button" className="text-gray-500 hover:text-gray-900" onClick={() => onEdit(item)}>
                        Editar
                      </button>
                    )}
                    {onDelete && (
                      <button type="button" className="text-gray-500 hover:text-red-600" onClick={() => onDelete(item)}>
                        Eliminar
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
