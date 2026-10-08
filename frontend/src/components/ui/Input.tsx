import { forwardRef, useId, type InputHTMLAttributes } from 'react'

type Props = InputHTMLAttributes<HTMLInputElement> & {
  label: string
  error?: string
  /** Help shown under the field while it has no error. */
  hint?: string
}

export const Input = forwardRef<HTMLInputElement, Props>(({ label, error, hint, id, className = '', ...props }, ref) => {
  const generatedId = useId()
  const inputId = id ?? props.name ?? generatedId
  const describedBy = error ? `${inputId}-error` : hint ? `${inputId}-hint` : undefined

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={inputId} className="text-sm font-medium text-gray-700">
        {label}
        {props.required && <span className="text-red-500" aria-hidden="true"> *</span>}
      </label>
      <input
        ref={ref}
        id={inputId}
        className={`field ${className}`}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        {...props}
      />
      {error ? (
        <span id={`${inputId}-error`} className="text-sm text-red-600">
          {error}
        </span>
      ) : (
        hint && (
          <span id={`${inputId}-hint`} className="text-xs text-gray-500">
            {hint}
          </span>
        )
      )}
    </div>
  )
})
Input.displayName = 'Input'
