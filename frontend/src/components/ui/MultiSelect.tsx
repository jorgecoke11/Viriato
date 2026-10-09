import { ChevronDown, X } from 'lucide-react'
import { useState } from 'react'
import { filtrarOpciones, marcarTodas, resumirSeleccion, type Opcion } from '../../lib/opciones'
import { Button } from './Button'
import { Checkbox } from './Checkbox'
import { Popover } from './Popover'
import { SearchField } from './SearchField'

const CHIPS_VISIBLES = 6

interface Props {
  /** What is being chosen ("Procesos visibles"), for the field and for a screen reader. */
  etiqueta: string
  opciones: readonly Opcion[]
  seleccion: ReadonlySet<string>
  alCambiar: (seleccion: Set<string>) => void
  /** "procesos" / "proceso": for the summary on the button. */
  plural: string
  singular: string
  /** A line under the field that says what picking does. */
  ayuda?: string
}

function Lista({ etiqueta, opciones, seleccion, alCambiar }: Pick<Props, 'etiqueta' | 'opciones' | 'seleccion' | 'alCambiar'>) {
  const [texto, setTexto] = useState('')
  const coincidentes = filtrarOpciones(opciones, texto)
  const idsCoincidentes = coincidentes.map((o) => o.id)
  const marcadas = coincidentes.filter((o) => seleccion.has(o.id)).length

  return (
    <div className="flex flex-col gap-2.5">
      <SearchField label={`Buscar en ${etiqueta.toLowerCase()}`} placeholder="Buscar…" value={texto} onChange={setTexto} autoFocus />

      <div className="flex items-center justify-between gap-2 text-xs">
        <span className="num text-gray-500">
          {texto.trim() === '' ? `${seleccion.size} de ${opciones.length}` : `${coincidentes.length} coinciden`}
        </span>
        <span className="flex items-center gap-3">
          <button
            type="button"
            className="font-medium text-indigo-600 hover:text-indigo-700 disabled:text-gray-400"
            disabled={coincidentes.length === 0 || marcadas === coincidentes.length}
            onClick={() => alCambiar(marcarTodas(seleccion, idsCoincidentes, true))}
          >
            {texto.trim() === '' ? 'Marcar todos' : 'Marcar los que coinciden'}
          </button>
          <button
            type="button"
            className="font-medium text-indigo-600 hover:text-indigo-700 disabled:text-gray-400"
            disabled={marcadas === 0}
            onClick={() => alCambiar(marcarTodas(seleccion, idsCoincidentes, false))}
          >
            {texto.trim() === '' ? 'Quitar todos' : 'Quitar los que coinciden'}
          </button>
        </span>
      </div>

      {coincidentes.length === 0 ? (
        <p className="py-4 text-center text-sm text-gray-500">Nada coincide con «{texto.trim()}».</p>
      ) : (
        <ul className="-mx-1 flex max-h-64 flex-col overflow-y-auto">
          {coincidentes.map((o) => (
            <li key={o.id}>
              <label className="flex cursor-pointer items-center gap-2.5 rounded-lg px-2 py-1.5 text-sm text-gray-800 hover:bg-gray-100">
                <Checkbox
                  label={o.etiqueta}
                  checked={seleccion.has(o.id)}
                  onChange={() => alCambiar(marcarTodas(seleccion, [o.id], !seleccion.has(o.id)))}
                />
                <span className="min-w-0 truncate" title={o.etiqueta}>
                  {o.etiqueta}
                </span>
              </label>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

/**
 * Picks several things out of a list that may be long — a hundred processes — without the list taking over the screen. It is one
 * field: it says how many are chosen, and opens a panel with a search, "mark all / none" and the list to tick. What is chosen,
 * when it is only some of them, is shown as chips that fold away after a few and can be taken off one by one.
 */
export function MultiSelect({ etiqueta, opciones, seleccion, alCambiar, plural, singular, ayuda }: Props) {
  const [verTodas, setVerTodas] = useState(false)
  const elegidas = opciones.filter((o) => seleccion.has(o.id))
  const sonAlgunas = elegidas.length > 0 && elegidas.length < opciones.length
  const chips = verTodas ? elegidas : elegidas.slice(0, CHIPS_VISIBLES)

  return (
    <div className="flex flex-col gap-2">
      <span className="text-sm font-medium text-gray-700">{etiqueta}</span>

      <Popover
        label={etiqueta}
        width={360}
        trigger={(props) => (
          <button
            type="button"
            ref={props.ref}
            onClick={props.onClick}
            aria-haspopup={props['aria-haspopup']}
            aria-expanded={props['aria-expanded']}
            aria-controls={props['aria-controls']}
            className="field flex w-full items-center justify-between gap-2 text-left"
          >
            <span className="min-w-0 truncate text-gray-900">{resumirSeleccion(elegidas.length, opciones.length, plural, singular)}</span>
            <ChevronDown size={16} aria-hidden="true" className={`shrink-0 text-gray-500 transition-transform ${props.open ? 'rotate-180' : ''}`} />
          </button>
        )}
      >
        {() => <Lista etiqueta={etiqueta} opciones={opciones} seleccion={seleccion} alCambiar={alCambiar} />}
      </Popover>

      {ayuda && !sonAlgunas && <p className="text-xs text-gray-500">{ayuda}</p>}

      {sonAlgunas && (
        <div className="flex flex-col gap-1.5">
          <ul className="flex flex-wrap gap-1.5" aria-label={`${etiqueta} elegidos`}>
            {chips.map((o) => (
              <li key={o.id} className="inline-flex max-w-full items-center gap-1 rounded-full bg-indigo-100 py-0.5 pr-1 pl-2.5 text-xs font-medium text-indigo-700">
                <span className="truncate" title={o.etiqueta}>
                  {o.etiqueta}
                </span>
                <button
                  type="button"
                  aria-label={`Quitar ${o.etiqueta}`}
                  className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full hover:bg-indigo-200"
                  onClick={() => alCambiar(marcarTodas(seleccion, [o.id], false))}
                >
                  <X size={12} aria-hidden="true" />
                </button>
              </li>
            ))}
          </ul>
          {elegidas.length > CHIPS_VISIBLES && (
            <Button type="button" variant="ghost" size="sm" className="self-start" aria-expanded={verTodas} onClick={() => setVerTodas((v) => !v)}>
              {verTodas ? 'Ver menos' : `Ver los ${elegidas.length}`}
            </Button>
          )}
        </div>
      )}
    </div>
  )
}
