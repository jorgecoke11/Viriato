import type { ResumenFilters } from './api'

// "Solo hoy" and "todos" are really the same question as a custom Desde/Hasta range — which
// finalized Casos to show — just at different levels of detail. One type, one modal, instead of two
// competing controls. Casos en curso are never affected by any of these (see the backend endpoint).
export type FinalizadosFiltro = { tipo: 'hoy' } | { tipo: 'todos' } | { tipo: 'rango'; desde?: string; hasta?: string }

export function filtroToParams(filtro: FinalizadosFiltro): Pick<ResumenFilters, 'finalizados' | 'desde' | 'hasta'> {
  if (filtro.tipo === 'todos') return { finalizados: 'todos' }
  if (filtro.tipo === 'rango') return { desde: filtro.desde, hasta: filtro.hasta }
  return {}
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
