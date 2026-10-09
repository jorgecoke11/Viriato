import { CalendarRange, Check } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import {
  aDia,
  describirDias,
  describirRango,
  esDia,
  etiquetaDeAtajo,
  resolverRango,
  TODOS_LOS_ATAJOS,
  type AtajoDeFecha,
  type RangoElegido,
} from '../../lib/rangoDeFechas'
import { Button } from './Button'
import { Calendario, type RangoEnCalendario } from './Calendario'
import { Popover } from './Popover'

/** What a custom trigger spreads onto its button to be a proper disclosure button for the panel. */
export interface PropiedadesDeDisparo {
  open: boolean
  onClick: () => void
  ref: (el: HTMLElement | null) => void
  'aria-haspopup': 'dialog'
  'aria-expanded': boolean
  'aria-controls': string
}

interface Props {
  /** Names the panel ("Finalizados") for people using a screen reader. */
  titulo: string
  valor: RangoElegido
  alCambiar: (valor: RangoElegido) => void
  /** The shortcuts offered, in order. */
  atajos?: readonly AtajoDeFecha[]
  /** The word for "no limit" ("Todos los finalizados"); null if that choice is not offered. */
  textoDeTodos?: string | null
  /** The last day that can be picked; today by default. Pass null for no limit. */
  maximo?: string | null
  /** What the default button says; by default, the chosen range. */
  resumen?: string
  /** Draws the button that opens it, when the default one does not fit (an icon button in a card, for example). It gets the chosen
   *  range as it reads on a button. */
  disparador?: (props: PropiedadesDeDisparo, resumen: string) => ReactNode
  /** Extra actions at the foot of the panel; `cerrar` closes it. */
  pie?: (cerrar: () => void) => ReactNode
  /** A line above the choices that says what they are for. */
  encabezado?: ReactNode
}

function Panel({
  valor,
  alCambiar,
  atajos,
  textoDeTodos,
  maximo,
  pie,
  encabezado,
  cerrar,
}: Required<Pick<Props, 'valor' | 'alCambiar' | 'atajos'>> & Pick<Props, 'textoDeTodos' | 'pie' | 'encabezado'> & { maximo?: string; cerrar: () => void }) {
  const ahora = new Date()
  const actual = resolverRango(valor, ahora)
  // What is being picked in the calendar; nothing until the person clicks (the calendar shows the current choice meanwhile).
  const [borrador, setBorrador] = useState<RangoEnCalendario | null>(null)
  const completo = borrador !== null && borrador.desde !== undefined && borrador.hasta !== undefined
  const mostrado = borrador ?? actual

  function aplicar(nuevo: RangoElegido) {
    alCambiar(nuevo)
    cerrar()
  }

  const opcion = (activa: boolean, etiqueta: string, alPulsar: () => void) => (
    <button
      key={etiqueta}
      type="button"
      aria-pressed={activa}
      className={`flex w-full items-center justify-between gap-2 rounded-lg px-3 py-1.5 text-left text-sm ${
        activa ? 'bg-indigo-100 font-medium text-indigo-700' : 'text-gray-700 hover:bg-gray-100'
      }`}
      onClick={alPulsar}
    >
      {etiqueta}
      {activa && <Check size={14} aria-hidden="true" />}
    </button>
  )

  return (
    <div className="flex flex-col gap-3">
      {encabezado && <p className="text-sm text-gray-600">{encabezado}</p>}
      <div className="grid gap-4 sm:grid-cols-[10.5rem_minmax(0,1fr)]">
        <div className="flex flex-col gap-0.5 sm:border-r sm:border-gray-200 sm:pr-3">
          {atajos.map((atajo) =>
            opcion(valor.tipo === 'atajo' && valor.atajo === atajo && borrador === null, etiquetaDeAtajo(atajo), () => aplicar({ tipo: 'atajo', atajo })),
          )}
          {textoDeTodos && opcion(valor.tipo === 'todos' && borrador === null, textoDeTodos, () => aplicar({ tipo: 'todos' }))}
        </div>
        <Calendario valor={mostrado} alCambiar={setBorrador} maximo={maximo} />
      </div>

      <div className="flex flex-wrap items-center justify-between gap-2 border-t border-gray-100 pt-3">
        <span className="text-sm text-gray-600">
          {borrador === null ? (
            valor.tipo === 'todos' ? (
              (textoDeTodos ?? 'Sin límite')
            ) : (
              <>
                <span className="text-gray-500">Ahora:</span> <span className="font-medium text-gray-900">{describirDias(actual.desde, actual.hasta, ahora)}</span>
              </>
            )
          ) : completo ? (
            <span className="font-medium text-gray-900">{describirDias(borrador.desde, borrador.hasta, ahora)}</span>
          ) : (
            <span className="text-gray-500">Elige el último día del rango</span>
          )}
        </span>
        <span className="flex items-center gap-2">
          {pie?.(cerrar)}
          <Button
            type="button"
            size="sm"
            disabled={!completo}
            onClick={() => completo && aplicar({ tipo: 'rango', desde: borrador.desde, hasta: borrador.hasta })}
          >
            Aplicar
          </Button>
        </span>
      </div>
    </div>
  )
}

/**
 * Picks a range of days: a few shortcuts that stay true as the days go by ("Últimos 7 días"), an optional "no limit", and a
 * calendar for anything else, in a panel that hangs from a button showing what is chosen. A shortcut applies at once; a range
 * from the calendar applies with «Aplicar». It does not know what it filters — that is the caller's.
 */
export function SelectorDeRango({ titulo, valor, alCambiar, atajos = TODOS_LOS_ATAJOS, textoDeTodos = null, maximo, resumen, disparador, pie, encabezado }: Props) {
  const limite = maximo === null ? undefined : maximo !== undefined && esDia(maximo) ? maximo : aDia(new Date())
  const texto = resumen ?? describirRango(valor, new Date(), textoDeTodos ?? 'Todo')
  const ancho = typeof window === 'undefined' ? 600 : Math.min(600, window.innerWidth - 24)

  return (
    <Popover
      label={titulo}
      width={ancho}
      trigger={(props) =>
        disparador ? (
          disparador(props, texto)
        ) : (
          <Button
            type="button"
            variant="secondary"
            ref={props.ref}
            onClick={props.onClick}
            aria-haspopup={props['aria-haspopup']}
            aria-expanded={props['aria-expanded']}
            aria-controls={props['aria-controls']}
          >
            <CalendarRange size={16} aria-hidden="true" />
            {texto}
          </Button>
        )
      }
    >
      {(cerrar) => <Panel valor={valor} alCambiar={alCambiar} atajos={atajos} textoDeTodos={textoDeTodos} maximo={limite} pie={pie} encabezado={encabezado} cerrar={cerrar} />}
    </Popover>
  )
}
