import type { PoliticaDespacho } from '../api'
import type { ServicioEnOrden } from './OrdenServicios'

export interface DespachoConfig {
  /** Kept as typed so an empty or half-typed box does not fight the person typing. */
  maxEjecucionesSimultaneas: string
  politica: PoliticaDespacho
  orden: ServicioEnOrden[]
}

export const MAX_SIMULTANEAS = 50

export type TopeLeido = { valido: true; valor: number | null } | { valido: false }

/**
 * The optional ceiling of simultaneous steps as the API wants it. An empty box is valid and means no ceiling (`valor` null):
 * the capacity is then the number of robot copies the stack runs. Anything else must be a whole number from 1 to 50.
 */
export function leerTope(texto: string): TopeLeido {
  if (texto.trim() === '') return { valido: true, valor: null }
  const n = Number(texto)
  return Number.isInteger(n) && n >= 1 && n <= MAX_SIMULTANEAS ? { valido: true, valor: n } : { valido: false }
}
