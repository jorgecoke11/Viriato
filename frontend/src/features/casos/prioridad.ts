/** The range the platform accepts for the priority of an execution. */
export const PRIORIDAD_MINIMA = -1000
export const PRIORIDAD_MAXIMA = 1000

/** The levels people actually reach for. Any whole number in range is valid; these are the shortcuts to the usual ones. */
export const NIVELES_DE_PRIORIDAD = [
  { valor: -10, nombre: 'Baja' },
  { valor: 0, nombre: 'Normal' },
  { valor: 10, nombre: 'Alta' },
  { valor: 100, nombre: 'Urgente' },
] as const

/**
 * What is wrong with the text typed as a priority, or null if it is fine. A whole number, between the limits: an
 * empty box is not a priority (there is no "none" — ordinary is 0), so it is reported instead of read as zero.
 */
export function validarPrioridad(texto: string): string | null {
  if (texto.trim() === '') return 'Escribe un número (0 es lo normal).'
  const n = Number(texto)
  if (!Number.isInteger(n)) return 'Debe ser un número entero.'
  if (n < PRIORIDAD_MINIMA || n > PRIORIDAD_MAXIMA) return `Debe estar entre ${PRIORIDAD_MINIMA} y ${PRIORIDAD_MAXIMA}.`
  return null
}

/** How a priority reads in a table: nothing for the ordinary one, and a sign so higher and lower are told apart. */
export function etiquetaPrioridad(prioridad: number): string | null {
  if (prioridad === 0) return null
  return prioridad > 0 ? `Prioridad +${prioridad}` : `Prioridad ${prioridad}`
}

export type TonoDePrioridad = 'alta' | 'normal' | 'baja'

/** A priority as a short name and a tone: "Urgente", "Alta" … for the usual levels, "+7" or "-3" for the rest. */
export function describirPrioridad(prioridad: number): { texto: string; tono: TonoDePrioridad } {
  const nivel = NIVELES_DE_PRIORIDAD.find((n) => n.valor === prioridad)
  const tono: TonoDePrioridad = prioridad > 0 ? 'alta' : prioridad < 0 ? 'baja' : 'normal'
  if (nivel) return { texto: nivel.nombre, tono }
  return { texto: prioridad > 0 ? `+${prioridad}` : String(prioridad), tono }
}
