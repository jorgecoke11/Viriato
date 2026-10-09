import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { hayFiltros as hayFiltrosEn, recogerIds, type ConsultaDeLista } from './lista'
import { estadoDeSeleccion, useSeleccion } from './seleccion'
import type { ResultadoMasivo } from './accionesMasivas'
import type { PagedResult } from './types'
import { useDebouncedValue } from './useDebouncedValue'

export interface OpcionesDeLista<T> {
  /** Names the list in the query cache; the question put to the server is added to it. */
  clave: readonly unknown[]
  /** Asks the server for one page. The list does not know how: that is the screen's. */
  cargar: (consulta: ConsultaDeLista) => Promise<PagedResult<T>>
  obtenerId: (fila: T) => string
  tamano?: number
  /** Where the filters start, and what "no filters" means for the button that clears them. */
  filtrosPorDefecto?: Readonly<Record<string, string>>
  /** The most that "select all that match" will take: past that, the list says to narrow it down. */
  maximoSeleccionable?: number
  /** How often to look again by itself (ms), or false; it gets what the list holds now. */
  refrescarCada?: (datos: PagedResult<T> | undefined) => number | false
  habilitada?: boolean
}

/**
 * The state of any list that lives on the server: a search (settled before it is sent), filters, the page, what is ticked (which
 * outlives page and filter changes) and "select all that match", which reads past the page on screen. It does not draw
 * anything — `ListaDeDatos` does, and so can anything else that needs the same behaviour.
 */
export function useListaPaginada<T>({
  clave,
  cargar,
  obtenerId,
  tamano = 25,
  filtrosPorDefecto = {},
  maximoSeleccionable = 1000,
  refrescarCada,
  habilitada = true,
}: OpcionesDeLista<T>) {
  const [busqueda, setBusqueda] = useState('')
  const [filtros, setFiltros] = useState<Record<string, string>>({ ...filtrosPorDefecto })
  const [pagina, setPagina] = useState(1)
  const [cargandoTodos, setCargandoTodos] = useState(false)
  // What the last action on the selection did, kept here so any part of the screen can show it.
  const [informe, setInforme] = useState<{ mensaje: string; resultado: ResultadoMasivo } | null>(null)
  const seleccion = useSeleccion()
  // Both are settled before they are sent: a text filter typed letter by letter must not ask the server for each one.
  const texto = useDebouncedValue(busqueda.trim(), 300)
  const filtrosAsentados = useDebouncedValue(filtros, 300)

  // Another search or filter is another list: back to its first page.
  const huella = JSON.stringify([texto, filtrosAsentados])
  useEffect(() => {
    setPagina(1)
  }, [huella])

  const consulta: ConsultaDeLista = useMemo(
    () => ({ busqueda: texto, filtros: filtrosAsentados, pagina, tamano }),
    [texto, filtrosAsentados, pagina, tamano],
  )

  const query = useQuery({
    queryKey: [...clave, consulta],
    queryFn: () => cargar(consulta),
    placeholderData: keepPreviousData,
    enabled: habilitada,
    refetchInterval: refrescarCada ? (q) => refrescarCada(q.state.data) : undefined,
  })

  const filas = useMemo(() => query.data?.items ?? [], [query.data])
  const total = query.data?.total ?? 0
  const idsDeLaPagina = useMemo(() => filas.map(obtenerId), [filas, obtenerId])
  const todaLaPaginaMarcada = estadoDeSeleccion(seleccion.ids, idsDeLaPagina) === 'todos'
  const quedanPorMarcar = total > seleccion.cantidad && total > tamano

  const ponerFiltro = useCallback((nombre: string, valor: string) => setFiltros((f) => ({ ...f, [nombre]: valor })), [])
  const quitarFiltros = useCallback(() => {
    setBusqueda('')
    setFiltros({ ...filtrosPorDefecto })
    // The defaults are fixed for a given list; only the call matters.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  /** Ticks everything that matches the search and filters now, not just this page (up to the maximum). */
  const seleccionarTodosLosQueCoinciden = useCallback(async () => {
    setCargandoTodos(true)
    try {
      const ids = await recogerIds(
        (p, t) => cargar({ ...consulta, pagina: p, tamano: t }),
        obtenerId,
        total,
        maximoSeleccionable,
      )
      seleccion.reemplazar(ids)
    } finally {
      setCargandoTodos(false)
    }
  }, [cargar, consulta, obtenerId, total, maximoSeleccionable, seleccion])

  return {
    filas,
    total,
    tamano,
    pagina,
    setPagina,
    busqueda,
    setBusqueda,
    filtros,
    ponerFiltro,
    quitarFiltros,
    hayFiltros: hayFiltrosEn(busqueda, filtros, filtrosPorDefecto),
    consulta,
    estado: {
      cargando: query.isLoading,
      conError: query.isError,
      correcta: query.isSuccess,
      actualizando: query.isPlaceholderData,
      reintentar: () => void query.refetch(),
    },
    seleccion,
    informe,
    setInforme,
    todaLaPaginaMarcada,
    quedanPorMarcar,
    seleccionarTodosLosQueCoinciden,
    cargandoTodos,
    maximoSeleccionable,
  }
}

export type ListaPaginada<T> = ReturnType<typeof useListaPaginada<T>>
