import type { ReactNode } from 'react'
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
        {items.map((item) => (
          <tr key={getId(item)} className="border-b border-gray-100 last:border-0">
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
          </tr>
        ))}
      </tbody>
    </table>
  )
}
