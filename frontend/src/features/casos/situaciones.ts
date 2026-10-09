import type { TipoCasoConteoDto } from './api'

/**
 * What a Caso is doing, in four groups that never overlap: running now, waiting in the queue, stopped (just started, paused or
 * waiting for a person) and over. They come from the Caso's technical estado — not from its business estado, which is another
 * question with its own filter — and they are the only vocabulary the dashboard counts, labels and filters with.
 */
export type GrupoDeCasos = 'ejecutando' | 'pendiente' | 'detenido' | 'finalizado'

interface Grupo {
  etiqueta: string
  /** The technical estados the group is made of, as the API names them. */
  estados: readonly string[]
}

export const GRUPOS: Record<GrupoDeCasos, Grupo> = {
  ejecutando: { etiqueta: 'En ejecución', estados: ['EnProgreso'] },
  pendiente: { etiqueta: 'Pendientes', estados: ['Pendiente'] },
  detenido: { etiqueta: 'Detenidos', estados: ['Iniciado', 'Pausado', 'EsperandoRevisionHumana'] },
  finalizado: { etiqueta: 'Finalizados', estados: ['Completado', 'Fallido', 'Cancelado'] },
}

/** Every technical estado with how the screens call it, in the order they are offered. */
const NOMBRES: Record<string, string> = {
  EnProgreso: 'En ejecución',
  Pendiente: 'Pendiente (en cola)',
  Iniciado: 'Iniciado',
  Pausado: 'Pausado',
  EsperandoRevisionHumana: 'Esperando revisión',
  Completado: 'Completado',
  Fallido: 'Fallido',
  Cancelado: 'Cancelado',
}

/** The value the list API takes for "any of these estados". */
export const valorDeGrupo = (grupo: GrupoDeCasos): string => GRUPOS[grupo].estados.join(',')

export interface OpcionDeSituacion {
  valor: string
  etiqueta: string
}

/**
 * The choices to narrow a list by how the Caso stands. Inside a group, only its own estados (plus the group itself when it has
 * more than one, so the narrowing can be undone); with no group — a list of one business estado — all of them.
 */
export function opcionesDeSituacion(grupo: GrupoDeCasos | null): OpcionDeSituacion[] {
  const estados = grupo === null ? Object.keys(NOMBRES) : GRUPOS[grupo].estados
  const propias = estados.map((e) => ({ valor: e, etiqueta: NOMBRES[e] ?? e }))
  if (grupo === null) return [{ valor: '', etiqueta: 'Cualquier situación' }, ...propias]
  return [{ valor: '', etiqueta: `Todos los ${GRUPOS[grupo].etiqueta.toLowerCase()}` }, ...propias]
}

export interface ConteoPorGrupo {
  ejecutando: number
  pendiente: number
  detenido: number
  finalizado: number
}

/** The four counts of one or many Tipos de caso, added up. */
export function contarPorGrupo(tipos: readonly TipoCasoConteoDto[]): ConteoPorGrupo {
  return tipos.reduce<ConteoPorGrupo>(
    (suma, t) => ({
      ejecutando: suma.ejecutando + t.enEjecucion,
      pendiente: suma.pendiente + t.pendientes,
      detenido: suma.detenido + t.detenidos,
      finalizado: suma.finalizado + t.finalizados,
    }),
    { ejecutando: 0, pendiente: 0, detenido: 0, finalizado: 0 },
  )
}
