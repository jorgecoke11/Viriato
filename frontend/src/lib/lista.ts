import { sinAcentos } from './opciones'
import type { PagedResult } from './types'

/**
 * What every paged list shares, apart from how it draws: the question put to the server (a search, some filters, a page) and the
 * way of ticking "everything that matches", which has to go past the page on screen. Pure, so each rule is tested without a screen.
 */

export interface ConsultaDeLista {
  /** What the search box holds, already settled (trimmed, and not changing any more). */
  busqueda: string
  /** One entry per filter; an empty text means "not filtering by it". */
  filtros: Readonly<Record<string, string>>
  /** From 1. */
  pagina: number
  tamano: number
}

/** The filters that really narrow the list: the ones with a value. */
export function filtrosActivos(filtros: Readonly<Record<string, string>>): Record<string, string> {
  return Object.fromEntries(Object.entries(filtros).filter(([, valor]) => valor !== ''))
}

/** Whether the list is narrowed by anything at all (a search or a filter), as opposed to showing everything it has. */
export function hayFiltros(busqueda: string, filtros: Readonly<Record<string, string>>, porDefecto: Readonly<Record<string, string>> = {}): boolean {
  if (busqueda.trim() !== '') return true
  return Object.entries(filtros).some(([clave, valor]) => valor !== (porDefecto[clave] ?? ''))
}

/**
 * A list that arrives whole (a short catalogue the server does not page) as one page of it: searched by the text each item gives
 * (ignoring case and accents) and cut to the page asked for. It lets such a list use the same table as every other.
 */
export function paginarEnCliente<T>(items: readonly T[], consulta: ConsultaDeLista, textoDe: (item: T) => string): PagedResult<T> {
  const buscado = sinAcentos(consulta.busqueda.trim())
  const coinciden = buscado === '' ? [...items] : items.filter((i) => sinAcentos(textoDe(i)).includes(buscado))
  const desde = (consulta.pagina - 1) * consulta.tamano
  return { items: coinciden.slice(desde, desde + consulta.tamano), page: consulta.pagina, pageSize: consulta.tamano, total: coinciden.length }
}

export interface PaginaDeIds<T> {
  items: readonly T[]
}

/**
 * The ids of everything that matches, a chunk at a time, up to `maximo`: what "select all that match" needs, since it is more
 * than the page on screen. It stops when it has as many as are expected (`total`, or `maximo` if lower) or when the server runs
 * out, so a list that shrinks while it is being read does not loop for ever.
 */
export async function recogerIds<T>(
  cargarTrozo: (pagina: number, tamano: number) => Promise<PaginaDeIds<T>>,
  obtenerId: (fila: T) => string,
  total: number,
  maximo: number,
  tamanoDeTrozo = 100,
): Promise<string[]> {
  const buscados = Math.min(total, maximo)
  const ids: string[] = []
  for (let pagina = 1; ids.length < buscados; pagina++) {
    const trozo = await cargarTrozo(pagina, tamanoDeTrozo)
    if (trozo.items.length === 0) break
    ids.push(...trozo.items.map(obtenerId))
  }
  return ids.slice(0, maximo)
}
