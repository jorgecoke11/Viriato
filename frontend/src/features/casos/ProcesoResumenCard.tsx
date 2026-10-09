import { useQuery } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { ArrowRight, CalendarRange, CheckCheck, ChevronRight, CircleCheck, CircleDashed, Hourglass, Pause, Play, Workflow } from 'lucide-react'
import { Fragment, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button } from '../../components/ui/Button'
import { FichaDeCategoria } from '../../components/ui/FichaDeCategoria'
import { IconButton } from '../../components/ui/IconButton'
import { SelectorDeRango } from '../../components/ui/SelectorDeRango'
import { spring } from '../../lib/motion/tokens'
import { esRangoElegido, rangoAParam, type RangoElegido } from '../../lib/rangoDeFechas'
import { collapseVariants } from '../../lib/motion/variants'
import { useAuth } from '../auth/useAuth'
import { usePreferencia } from '../auth/usePreferencia'
import * as casosApi from './api'
import type { EstadoConteoDto, FlujoResumenDto, TipoCasoConteoDto } from './api'
import { describeFiltro, filtroToParams, type FinalizadosFiltro } from './finalizadosFiltro'
import { ACCIONES_DE_PROCESO } from './accionesDeProceso'
import { contarPorGrupo, GRUPOS, type ConteoPorGrupo, type GrupoDeCasos } from './situaciones'

const FECHAS_DE_LA_LISTA: RangoElegido = { tipo: 'atajo', atajo: '30d' }

/** A saved card choice is either a range or nothing (the card follows the panel). */
const esRangoOMas = (valor: unknown): valor is FinalizadosFiltro | null => valor === null || esRangoElegido(valor)

export type TipoCasoFiltro =
  | { kind: 'grupo'; grupo: GrupoDeCasos }
  | { kind: 'estado'; codigo: string | null; display: string }

// One rule for the whole card, the same four groups everywhere: running now = indigo with a pulse, waiting in the queue = amber,
// stopped = gray, over = green. Each chip also carries its own icon, so it never rests on colour.
const chips: Record<GrupoDeCasos, { icono: typeof Play; activo: string; barra: string }> = {
  ejecutando: { icono: Play, activo: 'bg-indigo-100 text-indigo-700 hover:bg-indigo-200', barra: 'bg-indigo-500' },
  pendiente: { icono: Hourglass, activo: 'bg-amber-100 text-amber-700 hover:bg-amber-200', barra: 'bg-amber-400' },
  detenido: { icono: Pause, activo: 'bg-gray-200 text-gray-700 hover:bg-gray-300', barra: 'bg-gray-400' },
  finalizado: { icono: CheckCheck, activo: 'bg-green-100 text-green-700 hover:bg-green-200', barra: 'bg-green-500' },
}

function ConteoChip({ count, grupo, onClick }: { count: number; grupo: GrupoDeCasos; onClick: () => void }) {
  const { icono: Icon, activo } = chips[grupo]
  const etiqueta = GRUPOS[grupo].etiqueta

  return (
    <button
      type="button"
      disabled={count === 0}
      title={etiqueta}
      aria-label={`${etiqueta}: ${count}`}
      className={`inline-flex min-w-[3.25rem] items-center justify-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ${
        count === 0 ? 'bg-gray-100 text-gray-400' : activo
      } disabled:cursor-default`}
      onClick={(e) => {
        e.stopPropagation()
        onClick()
      }}
    >
      <Icon size={12} aria-hidden="true" className={grupo === 'ejecutando' && count > 0 ? 'motion-safe:animate-pulse' : ''} />
      <span className="num">{count}</span>
    </button>
  )
}

// The business estados of a type, in two blocks that cannot be mistaken for each other: the ones a Caso is still passing through
// (dashed circle, blue) and the ones it ends on (checked circle, green, a thicker edge). Shape and colour both say it.
function GrupoDeEstados({ final, estados, onSelect }: { final: boolean; estados: EstadoConteoDto[]; onSelect: (filtro: TipoCasoFiltro) => void }) {
  if (estados.length === 0) return null
  const Icono = final ? CircleCheck : CircleDashed
  const suma = estados.reduce((total, e) => total + e.count, 0)

  return (
    <section aria-label={final ? 'Estados finales' : 'Estados en curso'} className="flex flex-col gap-1">
      <h4
        className={`flex w-fit items-center gap-1.5 rounded-md px-2 py-0.5 text-xs font-semibold ${
          final ? 'bg-green-100 text-green-800' : 'bg-blue-50 text-blue-700'
        }`}
      >
        <Icono size={13} aria-hidden="true" />
        {final ? 'Finales' : 'En curso'}
        <span className="num font-mono font-medium opacity-80">{suma}</span>
      </h4>
      {estados.map((estado) => (
        <button
          key={estado.codigo ?? 'sin-estado'}
          type="button"
          className={`flex items-center gap-2.5 rounded-lg border px-3 py-2 text-left text-sm ${
            final
              ? 'border-green-300 border-l-4 border-l-green-600 bg-green-50 hover:bg-green-100'
              : 'border-dashed border-blue-200 border-l-[3px] border-l-blue-500 bg-surface hover:bg-blue-50'
          }`}
          onClick={() => onSelect({ kind: 'estado', codigo: estado.codigo, display: estado.display })}
        >
          <Icono size={14} aria-hidden="true" className={`shrink-0 ${final ? 'text-green-600' : 'text-blue-500'}`} />
          <span className={`min-w-0 flex-1 truncate ${final ? 'font-semibold text-green-800' : 'text-gray-700'}`}>{estado.display}</span>
          <span className="num font-mono text-xs font-medium text-gray-900">{estado.count}</span>
        </button>
      ))}
    </section>
  )
}

function TipoCasoRow({ tipo, onSelect }: { tipo: TipoCasoConteoDto; onSelect: (filtro: TipoCasoFiltro) => void }) {
  const [open, setOpen] = useState(false)
  const porEstado = [...tipo.porEstado].sort((a, b) => a.orden - b.orden)
  const conteo = contarPorGrupo([tipo])
  const total = tipo.enCurso + tipo.finalizados
  // A type is its letter and its name, in a quiet square; colour is kept for the state of the cases. "Sin tipo" is the discreet one.
  const nombreDeTipo = tipo.tipoCasoId === null ? null : tipo.nombre

  return (
    <div className={`rounded-xl border border-gray-200 bg-surface transition-shadow ${open ? 'shadow-card' : ''}`}>
      <div className="flex items-center gap-2 px-3 py-2">
        <button
          type="button"
          className="flex min-w-0 flex-1 items-center gap-2 py-1 text-left"
          onClick={() => setOpen((o) => !o)}
          aria-expanded={open}
        >
          <motion.span className="shrink-0 text-gray-400" animate={{ rotate: open ? 90 : 0 }} transition={spring.snappy}>
            <ChevronRight size={16} aria-hidden="true" />
          </motion.span>
          <FichaDeCategoria nombre={nombreDeTipo} />
          <span className="min-w-0 truncate text-sm font-semibold text-gray-900">{tipo.nombre}</span>
          <span className="num shrink-0 text-xs text-gray-500">{total}</span>
        </button>
        <span className="flex shrink-0 flex-wrap items-center justify-end gap-1.5">
          {/* Only the groups that have something are drawn, except "finalizados", which anchors the row. */}
          {(['ejecutando', 'pendiente', 'detenido', 'finalizado'] as const)
            .filter((g) => g === 'finalizado' || conteo[g] > 0)
            .map((g) => (
              <ConteoChip key={g} grupo={g} count={conteo[g]} onClick={() => onSelect({ kind: 'grupo', grupo: g })} />
            ))}
        </span>
      </div>

      <AnimatePresence initial={false}>
        {open && (
          <motion.div className="overflow-hidden" variants={collapseVariants} initial="initial" animate="animate" exit="exit">
            <div className="flex flex-col gap-3 px-3 pb-3 pl-9">
              {porEstado.length === 0 ? (
                <p className="py-1 text-sm text-gray-500">Sin casos.</p>
              ) : (
                <>
                  <p className="px-1 text-[11px] font-medium tracking-wide text-gray-500 uppercase">Estado de negocio</p>
                  <GrupoDeEstados final={false} estados={porEstado.filter((e) => !e.esFinal)} onSelect={onSelect} />
                  <GrupoDeEstados final estados={porEstado.filter((e) => e.esFinal)} onSelect={onSelect} />
                </>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  )
}

// The proportion of the four groups as one slim bar with its legend (a group with nothing in it is left out of the legend,
// except "finalizados", which anchors it).
function ProgresoBarra({ conteo }: { conteo: ConteoPorGrupo }) {
  const grupos = ['finalizado', 'ejecutando', 'pendiente', 'detenido'] as const
  const total = grupos.reduce((suma, g) => suma + conteo[g], 0)
  const pct = (n: number) => (total === 0 ? 0 : (n / total) * 100)

  return (
    <div className="flex flex-col gap-2">
      <div
        role="img"
        aria-label={grupos.map((g) => `${conteo[g]} ${GRUPOS[g].etiqueta.toLowerCase()}`).join(', ')}
        className="flex h-2 overflow-hidden rounded-full bg-gray-100"
      >
        {grupos.map((g) => (
          <div key={g} className={chips[g].barra} style={{ width: `${pct(conteo[g])}%` }} />
        ))}
      </div>
      <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-gray-500">
        {grupos
          .filter((g) => conteo[g] > 0 || g === 'finalizado')
          .map((g) => (
            <span key={g} className="flex items-center gap-1.5">
              <span aria-hidden="true" className={`h-2 w-2 rounded-full ${chips[g].barra}`} />
              {GRUPOS[g].etiqueta} <span className="num font-mono font-medium text-gray-800">{conteo[g]}</span>
            </span>
          ))}
      </div>
    </div>
  )
}

// The read on one process: who it is (name, how many cases), how they split between moving and settled, and one
// row per Tipo de caso to dig into. Actions that act *on this process* (today: start a new Caso, pick which
// finished cases to count) are icon buttons in the header, next to its name — never an item in the sidebar:
// "process -> its actions", not "menu -> a global action that happens to need a process picker".
export function ProcesoResumenCard({
  resumen,
  modoGlobal,
  onSelectTipo,
}: {
  resumen: FlujoResumenDto
  modoGlobal: FinalizadosFiltro
  /** `modo` is the finished-Casos window the card is showing right now — its own if it broke away from the
   * panel-wide one — so whatever opens next lists what was counted, not everything. */
  onSelectTipo: (tipo: TipoCasoConteoDto, filtro: TipoCasoFiltro, modo: FinalizadosFiltro) => void
}) {
  const navigate = useNavigate()
  const { can } = useAuth()
  // Which of the process's actions (add a case, change parameters…) has its window open.
  const [accionAbierta, setAccionAbierta] = useState<string | null>(null)
  const acciones = ACCIONES_DE_PROCESO.filter((a) => can(a.permiso) && (a.disponible?.(resumen) ?? true))
  // The card's own choice of finished Casos, if it broke away from the panel's; kept for the person, card by card.
  // The dates last picked to open the full list of this process' Casos (what the picker starts on next time).
  const [fechasDeLaLista, setFechasDeLaLista] = usePreferencia<RangoElegido>('casos:ver-todos:fechas', FECHAS_DE_LA_LISTA, esRangoElegido)
  const [overrideModo, setOverrideModo] = usePreferencia<FinalizadosFiltro | null>(`panel:finalizados:${resumen.flujoId}`, null, esRangoOMas)

  // Only fetches on its own when this card has broken away from the panel-wide setting — otherwise
  // it just uses the `resumen` the Dashboard already fetched, so most cards never make an extra request.
  const overrideQuery = useQuery({
    queryKey: ['casos-resumen-override', resumen.flujoId, overrideModo],
    queryFn: () => casosApi.getResumen({ flujoId: resumen.flujoId, ...filtroToParams(overrideModo as FinalizadosFiltro) }),
    enabled: overrideModo !== null,
    select: (data) => data[0],
  })

  const activo = overrideModo !== null ? (overrideQuery.data ?? resumen) : resumen
  const tipos = [...activo.porTipo].sort((a, b) => a.orden - b.orden)
  const conteo = contarPorGrupo(tipos)

  return (
    <section
      aria-label={resumen.flujoNombre}
      className="flex flex-col overflow-hidden rounded-2xl border border-gray-200 bg-surface shadow-card"
    >
      <div className="flex items-start gap-3 px-5 pt-5">
        <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-indigo-100 text-indigo-600">
          <Workflow size={22} aria-hidden="true" />
        </span>
        <div className="min-w-0 flex-1">
          <h2 className="truncate text-base font-semibold text-gray-900" title={resumen.flujoNombre}>
            {resumen.flujoNombre}
          </h2>
          <p className="text-sm text-gray-500">
            <span className="num font-mono font-medium text-gray-800">{activo.total}</span> {activo.total === 1 ? 'caso' : 'casos'}
          </p>
        </div>
        <div className="-mt-1 -mr-1 flex shrink-0 items-center gap-1">
          <SelectorDeRango
            titulo={`Finalizados — ${resumen.flujoNombre}`}
            valor={overrideModo ?? modoGlobal}
            alCambiar={setOverrideModo}
            textoDeTodos="Todos los finalizados"
            disparador={(props) => (
              <IconButton
                {...props}
                label={overrideModo ? `${describeFiltro(overrideModo)} (propio)` : `${describeFiltro(modoGlobal)} (general)`}
              >
                <CalendarRange size={18} />
                {overrideModo !== null && <span className="absolute top-2 right-2 h-2 w-2 rounded-full bg-amber-500 ring-2 ring-surface" />}
              </IconButton>
            )}
            pie={
              overrideModo !== null
                ? (cerrar) => (
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        setOverrideModo(null)
                        cerrar()
                      }}
                    >
                      Usar el ajuste general
                    </Button>
                  )
                : undefined
            }
          />
          {acciones.map((accion) => (
            <IconButton
              key={accion.id}
              variant={accion.principal ? 'primary' : 'ghost'}
              label={accion.etiqueta(resumen)}
              onClick={() => setAccionAbierta(accion.id)}
            >
              <accion.icono size={18} />
            </IconButton>
          ))}
        </div>
      </div>

      <div className="px-5 pt-4">
        <ProgresoBarra conteo={conteo} />
      </div>

      <div className="flex flex-1 flex-col gap-2 px-3 pt-3 pb-3">
        {tipos.length === 0 ? (
          <p className="px-3 py-2 text-sm text-gray-500">Sin casos en este rango.</p>
        ) : (
          tipos.map((tipo) => (
            <TipoCasoRow key={tipo.tipoCasoId ?? 'sin-tipo'} tipo={tipo} onSelect={(filtro) => onSelectTipo(tipo, filtro, overrideModo ?? modoGlobal)} />
          ))
        )}
      </div>

      {/* Every Caso of a process can be a huge query: before opening the list, the person says from which dates. */}
      <SelectorDeRango
        titulo={`Ver los casos de ${resumen.flujoNombre}`}
        valor={fechasDeLaLista}
        alCambiar={(fechas) => {
          setFechasDeLaLista(fechas)
          navigate(`/casos/lista?flujoId=${resumen.flujoId}&fechas=${rangoAParam(fechas)}`)
        }}
        textoDeTodos="Todas las fechas"
        encabezado="¿De qué fechas quieres ver los casos? Se filtran por la fecha en que se crearon."
        disparador={(props) => (
          <button
            {...props}
            type="button"
            className="group flex w-full items-center justify-between border-t border-gray-100 px-5 py-3 text-sm font-medium text-indigo-600 hover:bg-gray-50"
          >
            Ver todos los casos
            <ArrowRight size={16} className="transition-transform group-hover:translate-x-0.5" aria-hidden="true" />
          </button>
        )}
      />

      {acciones.map((accion) => (
        <Fragment key={accion.id}>
          {accion.ventana({ abierto: accionAbierta === accion.id, resumen, alCerrar: () => setAccionAbierta(null) })}
        </Fragment>
      ))}

    </section>
  )
}
