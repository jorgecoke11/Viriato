import type { HTMLAttributes } from 'react'

/** The surface everything sits on: one border, one soft shadow, the same corner. Padding is `p-6` unless a
 *  caller passes `p-0` (tables and lists that run edge to edge). */
export function Card({ children, className = '', ...props }: HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={`rounded-xl border border-gray-200 bg-surface p-6 shadow-card ${className}`} {...props}>
      {children}
    </div>
  )
}
