import type { AccionMasiva } from '../../lib/accionesMasivas'
import type { ListaPaginada } from '../../lib/useListaPaginada'
import { AccionMasivaBoton } from './AccionMasivaBoton'
import { BulkActionBar } from './BulkActionBar'
import { ResultadoMasivoAviso } from './ResultadoMasivoAviso'

/**
 * Once a whole page is ticked and there are more that match, offers to tick all of them (past the page on screen). It says
 * nothing otherwise. For a list with selection: put it right above the table.
 */
export function AvisoSeleccionarTodos<T>({ fuente }: { fuente: ListaPaginada<T> }) {
  if (!fuente.todaLaPaginaMarcada || !fuente.quedanPorMarcar) return null

  return (
    <div className="flex flex-wrap items-center gap-x-3 gap-y-1 border-b border-gray-200 bg-indigo-50/60 px-4 py-2 text-sm text-gray-700">
      <span>
        Están marcados los <span className="num font-medium">{fuente.filas.length}</span> de esta página.
      </span>
      <button
        type="button"
        disabled={fuente.cargandoTodos}
        className="font-medium text-indigo-600 hover:text-indigo-700 disabled:opacity-50"
        onClick={fuente.seleccionarTodosLosQueCoinciden}
      >
        {fuente.cargandoTodos
          ? 'Seleccionando…'
          : fuente.total > fuente.maximoSeleccionable
            ? `Seleccionar los primeros ${fuente.maximoSeleccionable} de los ${fuente.total} que coinciden`
            : `Seleccionar los ${fuente.total} que coinciden`}
      </button>
    </div>
  )
}

/** What the last action did, with why each thing it left alone was left alone. Nothing until an action has run. */
export function InformeDeAcciones<T>({ fuente }: { fuente: ListaPaginada<T> }) {
  if (!fuente.informe) return null
  return <ResultadoMasivoAviso mensaje={fuente.informe.mensaje} resultado={fuente.informe.resultado} alCerrar={() => fuente.setInforme(null)} />
}

/**
 * The bar that appears once something is ticked, with a button per action that can be done to the selection. Actions are data
 * (`AccionMasiva`): a list gets a new one by adding it to its array, with no change here.
 */
export function AccionesDeSeleccion<T>({
  fuente,
  acciones,
  entidad,
}: {
  fuente: ListaPaginada<T>
  acciones: readonly AccionMasiva[]
  /** What the rows are, to say "3 casos seleccionados". */
  entidad: { singular: string; plural: string }
}) {
  const { seleccion } = fuente

  return (
    <BulkActionBar cantidad={seleccion.cantidad} singular={entidad.singular} plural={entidad.plural} alLimpiar={seleccion.limpiar}>
      {acciones.map((accion) => (
        <AccionMasivaBoton
          key={accion.id}
          accion={accion}
          ids={[...seleccion.ids]}
          alTerminar={(resultado) => {
            fuente.setInforme({ mensaje: accion.mensajeDeExito(resultado), resultado })
            seleccion.limpiar()
          }}
        />
      ))}
    </BulkActionBar>
  )
}
