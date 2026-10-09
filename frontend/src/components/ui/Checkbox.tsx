import { useEffect, useRef, type InputHTMLAttributes } from 'react'

type Props = Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'aria-label'> & {
  /** Required: a box on its own says nothing to a screen reader about what it ticks. */
  label: string
  /** Neither ticked nor empty: some of what it stands for is selected. */
  indeterminate?: boolean
}

/** A tick box with an accessible name and the "some of them" state, for select-all headers and lists. */
export function Checkbox({ label, indeterminate = false, className = '', ...props }: Props) {
  const ref = useRef<HTMLInputElement>(null)

  useEffect(() => {
    if (ref.current) ref.current.indeterminate = indeterminate
  }, [indeterminate])

  return (
    <input
      ref={ref}
      type="checkbox"
      aria-label={label}
      aria-checked={indeterminate ? 'mixed' : undefined}
      className={`h-4 w-4 shrink-0 cursor-pointer rounded border-gray-300 accent-indigo-600 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 disabled:cursor-not-allowed disabled:opacity-50 ${className}`}
      {...props}
    />
  )
}
