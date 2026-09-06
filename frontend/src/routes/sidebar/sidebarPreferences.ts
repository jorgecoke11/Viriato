// Whether the sidebar is collapsed to an icon rail — a per-browser display preference, same spirit
// as features/casos/dashboardPreferences.ts. Defaults to expanded when nothing is stored yet.
const COLLAPSED_KEY = 'viriato:sidebar:collapsed'

export function getSidebarCollapsed(): boolean {
  try {
    return localStorage.getItem(COLLAPSED_KEY) === 'true'
  } catch {
    return false
  }
}

export function setSidebarCollapsed(collapsed: boolean) {
  try {
    localStorage.setItem(COLLAPSED_KEY, String(collapsed))
  } catch {
    // Private browsing / storage disabled — the preference just won't persist.
  }
}
