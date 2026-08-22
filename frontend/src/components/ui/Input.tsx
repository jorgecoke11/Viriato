import { forwardRef, type InputHTMLAttributes } from 'react'

type Props = InputHTMLAttributes<HTMLInputElement> & {
  label: string
  error?: string
}

export const Input = forwardRef<HTMLInputElement, Props>(({ label, error, id, className = '', ...props }, ref) => {
  const inputId = id ?? props.name

  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={inputId} className="text-sm font-medium text-gray-700">
        {label}
      </label>
      <input
        ref={ref}
        id={inputId}
        className={`rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-500 focus:outline-none ${className}`}
        {...props}
      />
      {error && <span className="text-sm text-red-600">{error}</span>}
    </div>
  )
})
Input.displayName = 'Input'
