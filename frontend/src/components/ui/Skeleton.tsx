import type { HTMLAttributes } from 'react'

/** A grey placeholder where content is about to appear, so the page keeps its shape instead of jumping. */
export function Skeleton({ className = '', ...props }: HTMLAttributes<HTMLDivElement>) {
  return <div aria-hidden="true" className={`rounded-md bg-gray-200/70 motion-safe:animate-pulse ${className}`} {...props} />
}

/** A few table-ish rows of skeleton, for a list that is still loading. */
export function SkeletonRows({ rows = 4 }: { rows?: number }) {
  return (
    <div role="status" aria-label="Cargando" className="flex flex-col gap-3 p-4">
      {Array.from({ length: rows }, (_, i) => (
        <Skeleton key={i} className="h-5" style={{ width: `${92 - ((i * 13) % 35)}%` }} />
      ))}
    </div>
  )
}
