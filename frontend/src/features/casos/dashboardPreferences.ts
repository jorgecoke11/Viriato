// Which dashboard cards the user has chosen to hide — a per-browser display preference, not a
// security boundary (AsignacionFlujo already controls what a user can even fetch). Deliberately
// local-only: which cards someone likes to see doesn't need to follow them across devices.
const STORAGE_KEY = 'viriato:dashboard:hidden-flujos'

export function getHiddenFlujoIds(): Set<string> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? new Set(JSON.parse(raw) as string[]) : new Set()
  } catch {
    return new Set()
  }
}

export function setHiddenFlujoIds(ids: Set<string>) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify([...ids]))
  } catch {
    // Private browsing / storage disabled — the preference just won't persist.
  }
}
