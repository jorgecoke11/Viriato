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
          className="flex h-10 w-10 items-center justify-center rounded-lg text-gray-500 hover:bg-blue-50 hover:text-blue-700"
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
    <div className="relative px-3 pb-2">
      <Search size={15} className="pointer-events-none absolute top-1/2 left-6 -translate-y-1/2 text-gray-400" />
      <input
        ref={inputRef}
        type="text"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder="Buscar en el menú…"
        aria-label="Buscar en el menú"
        className="w-full rounded-lg border border-gray-200 bg-white py-1.5 pr-3 pl-8 text-sm text-gray-700 placeholder:text-gray-400 focus:border-blue-400 focus:outline-none focus:ring-2 focus:ring-blue-100"
      />
    </div>
  )
}
