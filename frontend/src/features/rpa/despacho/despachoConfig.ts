import type { PoliticaDespacho } from '../api'
import type { ServicioEnOrden } from './OrdenServicios'

export interface DespachoConfig {
  /** Kept as typed so an empty or half-typed box does not fight the person typing. */
  maxEjecucionesSimultaneas: string
  politica: PoliticaDespacho
  orden: ServicioEnOrden[]
}

export const MAX_SIMULTANEAS = 50

/** The number of simultaneous steps as the API wants it, or null while the box does not hold a valid one. */
export function leerMaximo(texto: string): number | null {
  if (texto.trim() === '') return null
  const n = Number(texto)
  return Number.isInteger(n) && n >= 1 && n <= MAX_SIMULTANEAS ? n : null
}
