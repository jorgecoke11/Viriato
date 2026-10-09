import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { aDia, compararDias, deDia, ordenarDias, semanasDelMes, sumarDias, type Dia } from '../../lib/rangoDeFechas'
import { IconButton } from './IconButton'

const DIAS_DE_LA_SEMANA = ['L', 'M', 'X', 'J', 'V', 'S', 'D']

const titulo = (anio: number, mes: number) => {
  const texto = new Intl.DateTimeFormat('es-ES', { month: 'long', year: 'numeric' }).format(new Date(anio, mes, 1))
  return texto.charAt(0).toUpperCase() + texto.slice(1)
}

const nombreLargo = (dia: Dia) => new Intl.DateTimeFormat('es-ES', { dateStyle: 'full' }).format(deDia(dia))

export interface RangoEnCalendario {
  desde?: Dia
  hasta?: Dia
}

/**
 * A month calendar for picking a range of days: the first click is where it starts, the second where it ends (in whichever
 * order), and in between the days that would be covered are shaded. Clicking the same day twice picks that one day. It is the
 * calendar only — what the range means, and any shortcuts, belong to whoever uses it.
 *
 * Monday first, in Spanish. The arrow keys move between days. `maximo` is the last day that can be picked.
 */
export function Calendario({
  valor,
  alCambiar,
  maximo,
}: {
  valor: RangoEnCalendario
  /** Called with the day that starts a range (no `hasta` yet) and then with the complete range. */
  alCambiar: (rango: RangoEnCalendario) => void
  maximo?: Dia
}) {
  const hoy = aDia(new Date())
  const [mostrado, setMostrado] = useState(() => {
    const base = deDia(valor.desde ?? valor.hasta ?? hoy)
    return { anio: base.getFullYear(), mes: base.getMonth() }
  })
  // The day that started a range which is still being picked: until the second click, the range is open on that side.
  const [inicio, setInicio] = useState<Dia | null>(null)
  const [sobrevolado, setSobrevolado] = useState<Dia | null>(null)
  const contenedor = useRef<HTMLDivElement>(null)
  const enfocarDespues = useRef<Dia | null>(null)

  const semanas = semanasDelMes(mostrado.anio, mostrado.mes)

  // What is shaded: the range being picked (following the pointer) or the one chosen.
  const sombreado: { desde?: Dia; hasta?: Dia } =
    inicio !== null && sobrevolado !== null
      ? ordenarDias(inicio, sobrevolado)
      : { desde: valor.desde, hasta: valor.hasta ?? valor.desde }

  useEffect(() => {
    if (enfocarDespues.current === null) return
    contenedor.current?.querySelector<HTMLButtonElement>(`[data-dia="${enfocarDespues.current}"]`)?.focus()
    enfocarDespues.current = null
  })

  function irA(dia: Dia) {
    const fecha = deDia(dia)
    setMostrado({ anio: fecha.getFullYear(), mes: fecha.getMonth() })
  }

  function mover(meses: number) {
    setMostrado((m) => {
      const fecha = new Date(m.anio, m.mes + meses, 1)
      return { anio: fecha.getFullYear(), mes: fecha.getMonth() }
    })
  }

  function elegir(dia: Dia) {
    if (inicio === null) {
      setInicio(dia)
      alCambiar({ desde: dia, hasta: undefined })
    } else {
      setInicio(null)
      setSobrevolado(null)
      alCambiar(ordenarDias(inicio, dia))
    }
  }

  function alPulsar(evento: KeyboardEvent<HTMLButtonElement>, dia: Dia) {
    const paso = { ArrowLeft: -1, ArrowRight: 1, ArrowUp: -7, ArrowDown: 7 }[evento.key]
    if (paso === undefined) return
    evento.preventDefault()
    const destino = sumarDias(dia, paso)
    if (maximo && compararDias(destino, maximo) > 0) return
    enfocarDespues.current = destino
    irA(destino)
    setSobrevolado(inicio !== null ? destino : null)
  }

  const noHayMasAdelante = maximo !== undefined && compararDias(aDia(new Date(mostrado.anio, mostrado.mes + 1, 1)), maximo) > 0

  return (
    <div ref={contenedor} className="select-none" onMouseLeave={() => setSobrevolado(null)}>
      <div className="mb-2 flex items-center justify-between">
        <IconButton label="Mes anterior" size="sm" onClick={() => mover(-1)}>
          <ChevronLeft size={18} />
        </IconButton>
        <span className="text-sm font-semibold text-gray-900" aria-live="polite">
          {titulo(mostrado.anio, mostrado.mes)}
        </span>
        <IconButton label="Mes siguiente" size="sm" disabled={noHayMasAdelante} onClick={() => mover(1)}>
          <ChevronRight size={18} />
        </IconButton>
      </div>

      <div className="grid grid-cols-7 text-center text-[11px] font-medium text-gray-500">
        {DIAS_DE_LA_SEMANA.map((d) => (
          <span key={d} aria-hidden="true" className="py-1">
            {d}
          </span>
        ))}
      </div>

      <div role="grid" aria-label={titulo(mostrado.anio, mostrado.mes)} className="grid grid-cols-7">
        {semanas.flat().map(({ dia, delMes }, i) => {
          const esInicio = sombreado.desde === dia
          const esFin = sombreado.hasta === dia
          const dentro = sombreado.desde !== undefined && sombreado.hasta !== undefined && compararDias(dia, sombreado.desde) >= 0 && compararDias(dia, sombreado.hasta) <= 0
          const extremo = esInicio || esFin
          const deshabilitado = maximo !== undefined && compararDias(dia, maximo) > 0
          const columna = i % 7

          return (
            <div
              key={dia}
              role="gridcell"
              className={`flex justify-center py-0.5 ${dentro && !(esInicio && esFin) ? `bg-indigo-100 ${esInicio || columna === 0 ? 'rounded-l-lg' : ''} ${esFin || columna === 6 ? 'rounded-r-lg' : ''}` : ''}`}
            >
              <button
                type="button"
                data-dia={dia}
                disabled={deshabilitado}
                aria-label={nombreLargo(dia)}
                aria-pressed={extremo}
                tabIndex={extremo || (sombreado.desde === undefined && dia === hoy) ? 0 : -1}
                className={`num h-9 w-9 rounded-lg text-sm transition-colors focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-indigo-500 disabled:cursor-not-allowed disabled:text-gray-300 ${
                  extremo
                    ? 'bg-indigo-600 font-semibold text-white'
                    : dentro
                      ? 'text-indigo-700 hover:bg-indigo-200'
                      : `${delMes ? 'text-gray-900' : 'text-gray-400'} enabled:hover:bg-gray-100 ${dia === hoy ? 'font-semibold ring-1 ring-indigo-400 ring-inset' : ''}`
                }`}
                onClick={() => elegir(dia)}
                onMouseEnter={() => inicio !== null && setSobrevolado(dia)}
                onFocus={() => inicio !== null && setSobrevolado(dia)}
                onKeyDown={(e) => alPulsar(e, dia)}
              >
                {deDia(dia).getDate()}
              </button>
            </div>
          )
        })}
      </div>
    </div>
  )
}
