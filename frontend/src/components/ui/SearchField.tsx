import { Search } from 'lucide-react'
import type { InputHTMLAttributes } from 'react'

type Props = Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'aria-label' | 'value' | 'onChange'> & {
  /** What the box searches, for a screen reader (the placeholder is not a name). */
  label: string
  value: string
  onChange: (valor: string) => void
}

/** A search box: a magnifier, the shared field style, and an accessible name. */
export function SearchField({ label, value, onChange, className = '', ...props }: Props) {
  return (
    <label className={`relative block ${className}`}>
      <span className="sr-only">{label}</span>
      <Search size={15} aria-hidden="true" className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-gray-400" />
      <input type="search" className="field pl-9" value={value} onChange={(e) => onChange(e.target.value)} {...props} />
    </label>
  )
}
