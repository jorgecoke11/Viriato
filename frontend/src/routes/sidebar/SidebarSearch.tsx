import { Search } from 'lucide-react'
import { useRef } from 'react'

/**
 * Hidden entirely in icon-rail mode (no room for it there) — a lone search icon takes its place and,
 * when clicked, expands the sidebar and hands focus to the real input once it has room to appear.
 */
export function SidebarSearch({
  collapsed,
  value,
  onChange,
  onRequestExpand,
}: {
  collapsed: boolean
  value: string
  onChange: (value: string) => void
  onRequestExpand?: () => void
}) {
  const inputRef = useRef<HTMLInputElement>(null)

  if (collapsed) {
    return (
      <div className="flex justify-center px-2 pb-2">
        <button
          type="button"
          aria-label="Buscar en el menú"
          title="Buscar"
          className="flex h-10 w-10 items-center justify-center rounded-lg text-side-text hover:bg-side-hover hover:text-side-strong"
          onClick={() => {
            onRequestExpand?.()
            requestAnimationFrame(() => inputRef.current?.focus())
          }}
        >
          <Search size={18} />
        </button>
      </div>
    )
  }

  return (
    <div className="relative px-3 pb-3">
      <Search size={15} className="pointer-events-none absolute top-1/2 left-6 -translate-y-1/2 text-side-text" aria-hidden="true" />
      <input
        ref={inputRef}
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder="Buscar en el menú…"
        aria-label="Buscar en el menú"
        className="h-9 w-full rounded-lg border border-side-border bg-side-hover/50 pr-3 pl-8 text-sm text-side-strong placeholder:text-side-text focus:border-indigo-400 focus:bg-side-hover focus:outline-none focus:ring-2 focus:ring-indigo-500/30"
      />
    </div>
  )
}
