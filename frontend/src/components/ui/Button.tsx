import type { ButtonHTMLAttributes } from 'react'

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: 'primary' | 'ghost' | 'danger'
}

export function Button({ variant = 'primary', className = '', ...props }: Props) {
  const base =
    'rounded-lg px-4 py-2 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-1 focus-visible:ring-indigo-400 active:scale-[0.98] disabled:opacity-50 disabled:cursor-not-allowed disabled:active:scale-100'
  const variants = {
    primary: 'bg-indigo-600 text-white shadow-sm hover:bg-indigo-500',
    ghost: 'bg-transparent text-gray-700 hover:bg-gray-100',
    // Reserved for actions with real, hard-to-reverse consequences (e.g. cancelling a caso) — pair
    // it with a ConfirmDialog rather than using it just to "make a button stand out".
    danger: 'bg-transparent text-red-600 hover:bg-red-50',
  }

  return <button className={`${base} ${variants[variant]} ${className}`} {...props} />
}
