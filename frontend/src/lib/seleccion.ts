import { useCallback, useMemo, useState } from 'react'

export type EstadoDeSeleccion = 'ninguno' | 'algunos' | 'todos'

/** The selection with `id` flipped. Never mutates the one it is given. */
export function alternar(seleccion: ReadonlySet<string>, id: string): Set<string> {
  const siguiente = new Set(seleccion)
  if (siguiente.has(id)) siguiente.delete(id)
  else siguiente.add(id)
  return siguiente
}

/** The selection with all of `ids` selected (`marcar`) or all of them left out. */
export function marcarVarios(seleccion: ReadonlySet<string>, ids: readonly string[], marcar: boolean): Set<string> {
  const siguiente = new Set(seleccion)
  for (const id of ids) {
    if (marcar) siguiente.add(id)
    else siguiente.delete(id)
  }
  return siguiente
}

/** How much of what is on screen is selected: what a "select all" checkbox shows (empty, dashed or ticked). */
export function estadoDeSeleccion(seleccion: ReadonlySet<string>, visibles: readonly string[]): EstadoDeSeleccion {
  if (visibles.length === 0) return 'ninguno'
  const marcadas = visibles.filter((id) => seleccion.has(id)).length
  if (marcadas === 0) return 'ninguno'
  return marcadas === visibles.length ? 'todos' : 'algunos'
}

/**
 * The state of a multiple selection of rows by id, for any list: it survives changing page or filter, so what has been
 * ticked is not lost on the way. The id helpers are pure (above) and tested on their own.
 */
export function useSeleccion() {
  const [ids, setIds] = useState<ReadonlySet<string>>(() => new Set())

  const alternarUna = useCallback((id: string) => setIds((s) => alternar(s, id)), [])
  const marcar = useCallback((lista: readonly string[], valor: boolean) => setIds((s) => marcarVarios(s, lista, valor)), [])
  const reemplazar = useCallback((lista: readonly string[]) => setIds(new Set(lista)), [])
  const quitar = useCallback((lista: readonly string[]) => setIds((s) => marcarVarios(s, lista, false)), [])
  const limpiar = useCallback(() => setIds(new Set()), [])

  return useMemo(
    () => ({ ids, cantidad: ids.size, alternar: alternarUna, marcar, reemplazar, quitar, limpiar }),
    [ids, alternarUna, marcar, reemplazar, quitar, limpiar],
  )
}

export type Seleccion = ReturnType<typeof useSeleccion>
