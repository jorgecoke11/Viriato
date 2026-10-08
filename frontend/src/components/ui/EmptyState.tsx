import type { ReactNode } from 'react'

/** What a list or panel shows when there is nothing to list: say why, and what to do about it. */
export function EmptyState({
  icon,
  title,
  description,
  action,
}: {
  icon?: ReactNode
  title: string
  description?: ReactNode
  action?: ReactNode
}) {
  return (
    <div className="flex flex-col items-center gap-3 px-6 py-10 text-center">
      {icon && <span className="flex h-12 w-12 items-center justify-center rounded-full bg-gray-100 text-gray-500">{icon}</span>}
      <div className="flex flex-col gap-1">
        <p className="text-sm font-medium text-gray-900">{title}</p>
        {description && <p className="max-w-sm text-sm text-gray-500">{description}</p>}
      </div>
      {action}
    </div>
  )
}
