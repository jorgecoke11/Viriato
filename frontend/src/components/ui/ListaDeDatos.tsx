import { Inbox } from 'lucide-react'
import type { ReactNode } from 'react'
import type { AccionMasiva } from '../../lib/accionesMasivas'
import type { ListaPaginada } from '../../lib/useListaPaginada'
import { AccionesDeSeleccion, AvisoSeleccionarTodos, InformeDeAcciones } from './AccionesDeLista'
import { Button } from './Button'
import { Card } from './Card'
import { DataTable, type ColumnaDeTabla } from './DataTable'
import { EmptyState } from './EmptyState'
import { Pagination } from './Pagination'
import { SearchField } from './SearchField'
import { SelectField, type OpcionDeSelect } from './SelectField'
import { Skeleton } from './Skeleton'

interface FiltroBase {
  /** The key it travels under in the list's filters. */
  clave: string
  /** What it narrows by, for a screen reader. */
  etiqueta: string
}

/** A filter: a drop-down (`opciones`), or a box for free text or a number. */
export type FiltroDeLista =
  | (FiltroBase & { tipo?: 'select'; opciones: readonly OpcionDeSelect[] })
  | (FiltroBase & { tipo: 'texto' | 'numero'; placeholder?: string })

interface Props<T> {
  /** The state of the list (see `useListaPaginada`): the rows, the search, the filters, the page, the selection. */
  fuente: ListaPaginada<T>
  columnas: readonly ColumnaDeTabla<T>[]
  obtenerId: (fila: T) => string
  /** What to call a row for a screen reader. */
  nombreDeFila: (fila: T) => string
  /** What the rows are, to say "3 casos seleccionados". */
  entidad: { singular: string; plural: string }
  /** The search box; `false` leaves it out. */
  buscador?: { etiqueta: string; placeholder?: string } | false
  filtros?: readonly FiltroDeLista[]
  /** More controls for the bar above the table (a date range, for instance). */
  barraExtra?: ReactNode
  /** What can be done to the selected rows. With none, the table has no selection column at all. */
  acciones?: readonly AccionMasiva[]
  /** What it says when there is nothing to list and nothing is filtering. */
  vacio?: { titulo?: string; descripcion?: string; icono?: ReactNode; accion?: ReactNode }
  alHacerClic?: (fila: T) => void
  anchoMinimo?: string
  className?: string
}

/**
 * The one way to show a list that lives on the server: a search box and filters above, the table (with the loading, error and
 * empty states), pages below, and — when it has actions — a selection column, the offer to select everything that matches (past the
 * page on screen) and the bar with a button per action, with the report of what each one did. What the list is, how it asks the
 * server and which actions exist are the screen's; the behaviour is the same for all of them.
 */
export function ListaDeDatos<T>({
  fuente,
  columnas,
  obtenerId,
  nombreDeFila,
  entidad,
  buscador = { etiqueta: 'Buscar' },
  filtros = [],
  barraExtra,
  acciones = [],
  vacio,
  alHacerClic,
  anchoMinimo,
  className = '',
}: Props<T>) {
  const conAcciones = acciones.length > 0
  const { filas, total, tamano, estado, seleccion } = fuente
  const hayBarra = buscador !== false || filtros.length > 0 || barraExtra !== undefined

  return (
    <div className={`flex flex-col gap-4 ${className}`}>
      <InformeDeAcciones fuente={fuente} />

      {hayBarra && (
        <div className="flex flex-wrap items-center gap-2">
          {buscador !== false && (
            <SearchField
              className="min-w-[14rem] flex-1"
              label={buscador.etiqueta}
              placeholder={buscador.placeholder ?? 'Buscar…'}
              value={fuente.busqueda}
              onChange={fuente.setBusqueda}
            />
          )}
          {filtros.map((filtro) =>
            'opciones' in filtro ? (
              <SelectField
                key={filtro.clave}
                className="w-full sm:w-56"
                label={filtro.etiqueta}
                value={fuente.filtros[filtro.clave] ?? ''}
                onChange={(valor) => fuente.ponerFiltro(filtro.clave, valor)}
                opciones={filtro.opciones}
              />
            ) : (
              <label key={filtro.clave} className="block w-full sm:w-48">
                <span className="sr-only">{filtro.etiqueta}</span>
                <input
                  className="field"
                  type={filtro.tipo === 'numero' ? 'number' : 'text'}
                  placeholder={filtro.placeholder ?? filtro.etiqueta}
                  value={fuente.filtros[filtro.clave] ?? ''}
                  onChange={(e) => fuente.ponerFiltro(filtro.clave, e.target.value)}
                />
              </label>
            ),
          )}
          {barraExtra}
        </div>
      )}

      <Card className="overflow-hidden p-0">
        {estado.cargando && (
          <div role="status" aria-label="Cargando" className="flex flex-col gap-2 p-4">
            <Skeleton className="h-10" />
            <Skeleton className="h-14" />
            <Skeleton className="h-14" />
          </div>
        )}

        {estado.conError && (
          <div className="flex items-center justify-between gap-3 p-4 text-sm">
            <span className="text-red-600">No se han podido cargar los datos.</span>
            <Button variant="secondary" size="sm" onClick={estado.reintentar}>
              Reintentar
            </Button>
          </div>
        )}

        {estado.correcta && filas.length === 0 && (
          <EmptyState
            icon={vacio?.icono ?? <Inbox size={22} />}
            title={fuente.hayFiltros ? `Ningún ${entidad.singular} coincide` : (vacio?.titulo ?? 'Sin resultados')}
            description={fuente.hayFiltros ? 'Prueba con otros filtros.' : vacio?.descripcion}
            action={
              fuente.hayFiltros ? (
                <Button variant="secondary" size="sm" onClick={fuente.quitarFiltros}>
                  Quitar filtros
                </Button>
              ) : (
                vacio?.accion
              )
            }
          />
        )}

        {filas.length > 0 && (
          <>
            {conAcciones && <AvisoSeleccionarTodos fuente={fuente} />}
            <DataTable
              filas={filas}
              columnas={columnas}
              obtenerId={obtenerId}
              seleccion={conAcciones ? seleccion : undefined}
              nombreDeFila={nombreDeFila}
              alHacerClic={conAcciones ? undefined : alHacerClic}
              atenuada={estado.actualizando}
              anchoMinimo={anchoMinimo}
            />
          </>
        )}
      </Card>

      <Pagination pagina={fuente.pagina} tamano={tamano} total={total} alCambiar={fuente.setPagina} />

      {conAcciones && <AccionesDeSeleccion fuente={fuente} acciones={acciones} entidad={entidad} />}
    </div>
  )
}
