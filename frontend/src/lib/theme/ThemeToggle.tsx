import { Monitor, Moon, Sun } from 'lucide-react'
import { useTheme, type Theme } from './themeContext'

const OPTIONS: { value: Theme; label: string; icon: typeof Sun }[] = [
  { value: 'light', label: 'Claro', icon: Sun },
  { value: 'system', label: 'Automático', icon: Monitor },
  { value: 'dark', label: 'Oscuro', icon: Moon },
]

/**
 * Light / automatic / dark. Meant for the dark navigation rail it lives in, so it is styled with the side-*
 * tokens. `compact` (the icon rail has no room for three buttons) is one button that steps to the next choice.
 */
export function ThemeToggle({ compact = false }: { compact?: boolean }) {
  const { theme, setTheme } = useTheme()

  if (compact) {
    const index = OPTIONS.findIndex((o) => o.value === theme)
    const next = OPTIONS[(index + 1) % OPTIONS.length]
    const current = OPTIONS[index]
    const Icon = current.icon
    return (
      <button
        type="button"
        title={`Tema: ${current.label} (cambiar a ${next.label.toLowerCase()})`}
        aria-label={`Tema: ${current.label}. Cambiar a ${next.label.toLowerCase()}`}
        className="flex h-10 w-10 items-center justify-center rounded-lg text-side-text hover:bg-side-hover hover:text-side-strong"
        onClick={() => setTheme(next.value)}
      >
        <Icon size={18} />
      </button>
    )
  }

  return (
    <div role="radiogroup" aria-label="Tema de la interfaz" className="flex rounded-lg border border-side-border bg-side p-0.5">
      {OPTIONS.map(({ value, label, icon: Icon }) => {
        const active = theme === value
        return (
          <button
            key={value}
            type="button"
            role="radio"
            aria-checked={active}
            title={label}
            aria-label={label}
            className={`flex h-8 flex-1 items-center justify-center rounded-md ${
              active ? 'bg-side-hover text-side-strong shadow-sm' : 'text-side-text hover:text-side-strong'
            }`}
            onClick={() => setTheme(value)}
          >
            <Icon size={15} />
          </button>
        )
      })}
    </div>
  )
}
