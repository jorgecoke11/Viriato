import { createContext, useContext } from 'react'

export type Theme = 'system' | 'light' | 'dark'

export interface ThemeContextValue {
  /** What the user chose. */
  theme: Theme
  /** What is actually showing. */
  resolved: 'light' | 'dark'
  setTheme: (theme: Theme) => void
}

export const ThemeContext = createContext<ThemeContextValue | null>(null)

export function useTheme(): ThemeContextValue {
  const context = useContext(ThemeContext)
  if (!context) throw new Error('useTheme must be used inside <ThemeProvider>.')
  return context
}
