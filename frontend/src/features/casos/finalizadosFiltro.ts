import { describirDias, finDelDia, inicioDelDia, resolverRango, type AtajoDeFecha, type RangoElegido } from '../../lib/rangoDeFechas'
import type { ListCasosFilters, ResumenFilters } from './api'

// Which finished Casos to show is one question — "finished when?" — whether the answer is a shortcut ("today", "the last 7
// days"), "no limit" or a custom range. It is the generic range of days of `lib/rangoDeFechas`, so the same picker serves every
// screen that filters by date. Casos still moving are never affected by any of these (see the backend endpoint).
export type FinalizadosFiltro = RangoElegido

export const FINALIZADOS_DE_HOY: FinalizadosFiltro = { tipo: 'atajo', atajo: 'hoy' }

// The days are the ones on the user's clock, so they are turned into instants here: left as bare dates the server
// would read them as midnight UTC, which cuts the last day of a range out and moves "today" by the time-zone offset.
export function filtroToParams(filtro: FinalizadosFiltro, ahora: Date = new Date()): Pick<ResumenFilters, 'finalizados' | 'desde' | 'hasta'> {
  if (filtro.tipo === 'todos') return { finalizados: 'todos' }
  const { desde, hasta } = resolverRango(filtro, ahora)
  return { desde: desde ? inicioDelDia(desde) : undefined, hasta: hasta ? finDelDia(hasta) : undefined }
}

/** The same window, named the way the Casos list takes it, for opening a group from the dashboard. */
export function filtroToListParams(
  filtro: FinalizadosFiltro,
  ahora: Date = new Date(),
): Pick<ListCasosFilters, 'ventana' | 'finalizados' | 'completadoDesde' | 'completadoHasta'> {
  const { finalizados, desde, hasta } = filtroToParams(filtro, ahora)
  return { ventana: true, finalizados, completadoDesde: desde, completadoHasta: hasta }
}

const DESCRIPCION_DE_ATAJO: Record<AtajoDeFecha, string> = {
  hoy: 'Finalizados de hoy',
  ayer: 'Finalizados de ayer',
  '7d': 'Finalizados, últimos 7 días',
  '30d': 'Finalizados, últimos 30 días',
  mes: 'Finalizados de este mes',
  mesPasado: 'Finalizados del mes pasado',
}

export function describeFiltro(filtro: FinalizadosFiltro, ahora: Date = new Date()): string {
  if (filtro.tipo === 'todos') return 'Todos los finalizados'
  if (filtro.tipo === 'atajo') return DESCRIPCION_DE_ATAJO[filtro.atajo]
  if (!filtro.desde && !filtro.hasta) return 'Finalizados: rango personalizado'
  return `Finalizados: ${describirDias(filtro.desde, filtro.hasta, ahora)}`
}
