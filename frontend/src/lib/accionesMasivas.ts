import type { LucideIcon } from 'lucide-react'

export interface ItemOmitido {
  id: string
  motivo: string
}

/** What a bulk action did, whatever the action: how many it was applied to and, for each one it was not, why not. */
export interface ResultadoMasivo {
  procesados: number
  omitidos: ItemOmitido[]
  /** Figures only this action has ("documentos": 37). Added up across the batches of a long selection. */
  datos?: Record<string, number>
  /** How many more were left alone beyond the ones named in `omitidos` (an action that reports only the first few). */
  omitidosSinDetalle?: number
}

/**
 * One thing that can be done to a selection of rows at once. It is data: the list of actions of a screen is an array of these,
 * and the screen draws a button for each with the same confirmation, pending state and report — so adding an action is adding
 * an entry here, not touching the screen. Nothing in it is about Casos: any list that can be ticked can have its own.
 */
export interface AccionMasiva {
  id: string
  /** The button, for this many selected: "Cancelar 3 casos". */
  etiqueta: (cantidad: number) => string
  icono: LucideIcon
  /** Drawn in red: it cannot be undone or throws work away. */
  peligrosa?: boolean
  /** The permission needed to see the action at all; absent = everyone who can reach the screen. */
  permiso?: string
  /** Asked before running it. `null` runs it right away, for what is harmless and easy to undo. */
  confirmacion: {
    titulo: (cantidad: number) => string
    mensaje: string
    etiquetaDeConfirmar: string
    etiquetaPendiente: string
    /** From this many selected, confirming means typing `texto`: not something to do by a stray click. */
    escribirDesde?: { cantidad: number; texto: string }
  } | null
  /** Runs it on the selected ids, in as many requests as it takes. */
  ejecutar: (ids: string[]) => Promise<ResultadoMasivo>
  /** The toast (and the headline of the report) once it has run. */
  mensajeDeExito: (resultado: ResultadoMasivo) => string
  /** The query keys to refresh afterwards. */
  invalidar: readonly string[]
}

/**
 * Runs a bulk request in batches (the server takes a limited number at a time) and adds up what each one did, in order. If a
 * batch fails the error goes up and the ones before it stay done: the caller sees the failure and refreshes the list, which
 * shows what is left.
 */
export async function ejecutarEnTandas(
  ids: readonly string[],
  tamano: number,
  ejecutarTanda: (ids: string[]) => Promise<ResultadoMasivo>,
): Promise<ResultadoMasivo> {
  const total: ResultadoMasivo = { procesados: 0, omitidos: [] }
  for (let i = 0; i < ids.length; i += tamano) {
    const parcial = await ejecutarTanda(ids.slice(i, i + tamano))
    total.procesados += parcial.procesados
    total.omitidos.push(...parcial.omitidos)
    if (parcial.omitidosSinDetalle) total.omitidosSinDetalle = (total.omitidosSinDetalle ?? 0) + parcial.omitidosSinDetalle
    for (const [clave, valor] of Object.entries(parcial.datos ?? {})) {
      total.datos = { ...total.datos, [clave]: (total.datos?.[clave] ?? 0) + valor }
    }
  }
  return total
}
