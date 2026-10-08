import type { ListCasosFilters, ResumenFilters } from './api'

// "Solo hoy" and "todos" are really the same question as a custom Desde/Hasta range — which
// finalized Casos to show — just at different levels of detail. One type, one modal, instead of two
// competing controls. Casos en curso are never affected by any of these (see the backend endpoint).
export type FinalizadosFiltro = { tipo: 'hoy' } | { tipo: 'todos' } | { tipo: 'rango'; desde?: string; hasta?: string }

/** The start of the given local day (yyyy-MM-dd), as the instant the server compares against. */
const inicioDelDia = (dia: string) => new Date(`${dia}T00:00:00`).toISOString()

/** The last instant of the given local day: a range "to the 7th" includes everything that happened on the 7th. */
const finDelDia = (dia: string) => new Date(`${dia}T23:59:59.999`).toISOString()

// The days are the ones on the user's clock, so they are turned into instants here: left as bare dates the server
// would read them as midnight UTC, which cuts the last day of a range out and moves "today" by the time-zone offset.
export function filtroToParams(filtro: FinalizadosFiltro, ahora: Date = new Date()): Pick<ResumenFilters, 'finalizados' | 'desde' | 'hasta'> {
  if (filtro.tipo === 'todos') return { finalizados: 'todos' }
  if (filtro.tipo === 'rango') {
    return { desde: filtro.desde ? inicioDelDia(filtro.desde) : undefined, hasta: filtro.hasta ? finDelDia(filtro.hasta) : undefined }
  }
  return { desde: new Date(ahora.getFullYear(), ahora.getMonth(), ahora.getDate()).toISOString() }
}

/** The same window, named the way the Casos list takes it, for opening a group from the dashboard. */
export function filtroToListParams(
  filtro: FinalizadosFiltro,
  ahora: Date = new Date(),
): Pick<ListCasosFilters, 'ventana' | 'finalizados' | 'completadoDesde' | 'completadoHasta'> {
  const { finalizados, desde, hasta } = filtroToParams(filtro, ahora)
  return { ventana: true, finalizados, completadoDesde: desde, completadoHasta: hasta }
}

export function describeFiltro(filtro: FinalizadosFiltro): string {
  if (filtro.tipo === 'todos') return 'Todos los finalizados'
  if (filtro.tipo === 'rango') {
    if (filtro.desde && filtro.hasta) return `Del ${filtro.desde} al ${filtro.hasta}`
    if (filtro.desde) return `Desde ${filtro.desde}`
    if (filtro.hasta) return `Hasta ${filtro.hasta}`
    return 'Rango personalizado'
  }
  return 'Finalizados de hoy'
}
