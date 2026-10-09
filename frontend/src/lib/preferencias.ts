/** What the part of the browser's storage we use looks like: only the three calls we make, so tests can pass a plain object. */
export interface Almacen {
  getItem(clave: string): string | null
  setItem(clave: string, valor: string): void
  removeItem(clave: string): void
}

const almacenDelNavegador = (): Almacen | null => {
  try {
    return typeof localStorage === 'undefined' ? null : localStorage
  } catch {
    // Storage can throw just by being touched (private mode, blocked site data).
    return null
  }
}

/** Where a person's preference lives: one key per person, so two people on the same browser keep their own. */
export const claveDePreferencia = (usuarioId: string | null | undefined, nombre: string) => `viriato:${usuarioId ?? 'anonimo'}:${nombre}`

/**
 * Reads a saved preference. Anything that is missing, unreadable, or not what the page expects (`valida` says so) is ignored
 * and `inicial` is used: a saved choice from an older version of the page must never break the new one.
 */
export function leerPreferencia<T>(clave: string, inicial: T, valida: (valor: unknown) => valor is T, almacen: Almacen | null = almacenDelNavegador()): T {
  if (almacen === null) return inicial
  try {
    const crudo = almacen.getItem(clave)
    if (crudo === null) return inicial
    const valor: unknown = JSON.parse(crudo)
    return valida(valor) ? valor : inicial
  } catch {
    return inicial
  }
}

/** Saves a preference. It is a convenience, not data: if storage is not there, the choice simply is not kept. */
export function guardarPreferencia<T>(clave: string, valor: T, almacen: Almacen | null = almacenDelNavegador()): void {
  if (almacen === null) return
  try {
    almacen.setItem(clave, JSON.stringify(valor))
  } catch {
    // Full or disabled: nothing useful to say to the person about it.
  }
}
