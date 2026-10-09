import { forwardRef, type ButtonHTMLAttributes } from 'react'

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  /**
   * primary    the one main action of a view (solid brand colour)
   * secondary  a normal action that is not the main one (outlined)
   * ghost      a quiet action: cancel, close, dismiss
   * danger     an action with consequences; pair it with a ConfirmDialog
   */
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger'
  size?: 'sm' | 'md'
}

const base =
  'inline-flex shrink-0 items-center justify-center gap-2 whitespace-nowrap rounded-lg font-medium select-none ' +
  'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 ' +
  'active:scale-[0.98] disabled:cursor-not-allowed disabled:opacity-50 disabled:active:scale-100'

const variants = {
  // brightness rather than a second shade: the same rule reads right on the light and the dark theme.
  primary: 'bg-indigo-600 text-white shadow-sm enabled:hover:brightness-110 enabled:active:brightness-95',
  secondary: 'border border-gray-300 bg-surface text-gray-700 shadow-sm enabled:hover:border-gray-400 enabled:hover:bg-gray-50',
  ghost: 'bg-transparent text-gray-700 enabled:hover:bg-gray-100',
  // Reserved for actions with real, hard-to-reverse consequences (e.g. cancelling a caso) — pair
  // it with a ConfirmDialog rather than using it just to "make a button stand out".
  danger: 'bg-transparent text-red-600 enabled:hover:bg-red-50',
}

const sizes = {
  sm: 'min-h-8 px-3 py-1 text-[13px]',
  md: 'min-h-10 px-4 py-2 text-sm',
}

export const Button = forwardRef<HTMLButtonElement, Props>(({ variant = 'primary', size = 'md', className = '', ...props }, ref) => (
  <button ref={ref} className={`${base} ${variants[variant]} ${sizes[size]} ${className}`} {...props} />
))
Button.displayName = 'Button'
