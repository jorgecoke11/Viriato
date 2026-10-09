// Which dashboard cards an older version let the person hide, saved for the whole browser. It is only read now, as the place the
// per-user preference starts from (see `usePreferencia`), so nobody has to hide their processes again.
const STORAGE_KEY = 'viriato:dashboard:hidden-flujos'

export function getHiddenFlujoIds(): Set<string> {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? new Set(JSON.parse(raw) as string[]) : new Set()
  } catch {
    return new Set()
  }
}
