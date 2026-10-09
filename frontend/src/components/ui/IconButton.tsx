import { forwardRef, type ButtonHTMLAttributes } from 'react'

type Props = Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'aria-label'> & {
  /** Required: an icon alone says nothing to a screen reader, and little to a person who has not met it yet. */
  label: string
  variant?: 'ghost' | 'secondary' | 'primary' | 'danger'
  /** `sm` for the actions of a table row, `md` (the default) elsewhere. */
  size?: 'sm' | 'md'
}

const variants = {
  ghost: 'text-gray-500 enabled:hover:bg-gray-100 enabled:hover:text-gray-900',
  secondary: 'border border-gray-200 bg-surface text-gray-600 shadow-sm enabled:hover:border-gray-300 enabled:hover:bg-gray-50 enabled:hover:text-gray-900',
  primary: 'bg-indigo-600 text-white shadow-sm enabled:hover:brightness-110',
  danger: 'text-gray-500 enabled:hover:bg-red-50 enabled:hover:text-red-600',
}

const sizes = { sm: 'h-9 w-9', md: 'h-10 w-10' }

/** A square icon button with a 40px hit area, a tooltip and an accessible name. */
export const IconButton = forwardRef<HTMLButtonElement, Props>(({ label, variant = 'ghost', size = 'md', className = '', children, ...props }, ref) => (
  <button
    ref={ref}
    type="button"
    aria-label={label}
    title={label}
    className={`relative inline-flex shrink-0 items-center justify-center rounded-lg active:scale-[0.97] disabled:cursor-not-allowed disabled:opacity-50 ${sizes[size]} ${variants[variant]} ${className}`}
    {...props}
  >
    {children}
  </button>
))
IconButton.displayName = 'IconButton'
