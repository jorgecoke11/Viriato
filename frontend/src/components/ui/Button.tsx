import type { ButtonHTMLAttributes } from 'react'

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: 'primary' | 'ghost'
}

export function Button({ variant = 'primary', className = '', ...props }: Props) {
  const base = 'rounded-md px-4 py-2 text-sm font-medium transition-colors disabled:opacity-50 disabled:cursor-not-allowed'
  const variants = {
    primary: 'bg-gray-900 text-white hover:bg-gray-700',
    ghost: 'bg-transparent text-gray-700 hover:bg-gray-100',
  }

  return <button className={`${base} ${variants[variant]} ${className}`} {...props} />
}
