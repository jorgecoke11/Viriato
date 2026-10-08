import { useQuery } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { Activity, ArrowRight, CalendarRange, CheckCheck, ChevronRight, Plus, Workflow } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { IconButton } from '../../components/ui/IconButton'
import { spring } from '../../lib/motion/tokens'
import { collapseVariants } from '../../lib/motion/variants'
import { useAuth } from '../auth/useAuth'
import * as casosApi from './api'
import type { EstadoConteoDto, FlujoResumenDto, TipoCasoConteoDto } from './api'
import { describeFiltro, filtroToParams, type FinalizadosFiltro } from './finalizadosFiltro'
import { FinalizadosFiltroModal } from './FinalizadosFiltroModal'
import { NuevoCasoModal } from './NuevoCasoModal'

export type TipoCasoFiltro =
  | { kind: 'bucket'; bucket: 'en-curso' | 'finalizado' }
  | { kind: 'estado'; codigo: string | null; display: string }

// One rule for the whole card: still moving = blue, settled = green. The same two colours the state badges use
// everywhere else (En progreso / Completado), and each chip also carries its own icon, so it never rests on colour.
function ConteoChip({ count, variant, onClick }: { count: number; variant: 'en-curso' | 'finalizado'; onClick: () => void }) {
  const enCurso = variant === 'en-curso'
  const Icon = enCurso ? Activity : CheckCheck
  const label = enCurso ? 'En curso' : 'Finalizados'

  return (
    <button
      type="button"
      disabled={count === 0}
      title={label}
      aria-label={`${label}: ${count}`}
      className={`inline-flex min-w-[3.25rem] items-center justify-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ${
        count === 0
          ? 'bg-gray-100 text-gray-400'
          : enCurso
            ? 'bg-blue-100 text-blue-700 hover:bg-blue-200'
            : 'bg-green-100 text-green-700 hover:bg-green-200'
      } disabled:cursor-default`}
      onClick={(e) => {
        e.stopPropagation()
        onClick()
      }}
    >
      <Icon size={12} aria-hidden="true" />
      <span className="num">{count}</span>
    </button>
  )
}

function TipoCasoRow({ tipo, onSelect }: { tipo: TipoCasoConteoDto; onSelect: (filtro: TipoCasoFiltro) => void }) {
  const [open, setOpen] = useState(false)
  const porEstado = [...tipo.porEstado].sort((a, b) => a.orden - b.orden)
  const total = tipo.enCurso + tipo.finalizados

  return (
    <div className={`rounded-xl transition-colors ${open ? 'bg-gray-50' : 'hover:bg-gray-50/70'}`}>
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
          <span className="min-w-0 truncate text-sm font-medium text-gray-900">{tipo.nombre}</span>
          <span className="num shrink-0 text-xs text-gray-400">{total}</span>
        </button>
        <span className="flex shrink-0 items-center gap-1.5">
          <ConteoChip count={tipo.enCurso} variant="en-curso" onClick={() => onSelect({ kind: 'bucket', bucket: 'en-curso' })} />
          <ConteoChip count={tipo.finalizados} variant="finalizado" onClick={() => onSelect({ kind: 'bucket', bucket: 'finalizado' })} />
        </span>
      </div>

      <AnimatePresence initial={false}>
        {open && (
          <motion.div className="overflow-hidden" variants={collapseVariants} initial="initial" animate="animate" exit="exit">
            <div className="flex flex-col gap-1 px-3 pb-3 pl-9">
              {porEstado.length === 0 ? (
                <p className="py-1 text-sm text-gray-500">Sin casos.</p>
              ) : (
                porEstado.map((estado: EstadoConteoDto) => (
                  <button
                    key={estado.codigo ?? 'sin-estado'}
                    type="button"
                    className="flex items-center gap-2.5 rounded-lg border border-gray-200 bg-surface px-3 py-2 text-left text-sm hover:border-gray-300 hover:bg-gray-50"
                    onClick={() => onSelect({ kind: 'estado', codigo: estado.codigo, display: estado.display })}
                  >
                    <span
                      aria-hidden="true"
                      className={`h-2 w-2 shrink-0 rounded-full ${estado.esFinal ? 'bg-green-500' : 'bg-blue-500'}`}
                    />
                    <span className="min-w-0 flex-1 truncate text-gray-700">{estado.display}</span>
                    <span className="num font-mono text-xs font-medium text-gray-900">{estado.count}</span>
                  </button>
                ))
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  )
}

// The proportion of what is still moving against what is settled, as one slim bar with its legend.
function ProgresoBarra({ enCurso, finalizados }: { enCurso: number; finalizados: number }) {
  const total = enCurso + finalizados
  const pct = (n: number) => (total === 0 ? 0 : (n / total) * 100)

  return (
    <div className="flex flex-col gap-2">
      <div
        role="img"
        aria-label={`${enCurso} en curso y ${finalizados} finalizados`}
        className="flex h-2 overflow-hidden rounded-full bg-gray-100"
      >
        <div className="bg-green-500" style={{ width: `${pct(finalizados)}%` }} />
        <div className="bg-blue-500" style={{ width: `${pct(enCurso)}%` }} />
      </div>
      <div className="flex items-center gap-4 text-xs text-gray-500">
        <span className="flex items-center gap-1.5">
          <span aria-hidden="true" className="h-2 w-2 rounded-full bg-blue-500" />
          En curso <span className="num font-mono font-medium text-gray-800">{enCurso}</span>
        </span>
        <span className="flex items-center gap-1.5">
          <span aria-hidden="true" className="h-2 w-2 rounded-full bg-green-500" />
          Finalizados <span className="num font-mono font-medium text-gray-800">{finalizados}</span>
        </span>
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
  const [showNuevoCaso, setShowNuevoCaso] = useState(false)
  const [showFiltroModal, setShowFiltroModal] = useState(false)
  const [overrideModo, setOverrideModo] = useState<FinalizadosFiltro | null>(null)

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
  const enCurso = tipos.reduce((suma, t) => suma + t.enCurso, 0)
  const finalizados = tipos.reduce((suma, t) => suma + t.finalizados, 0)

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
          <IconButton
            label={overrideModo ? `Finalizados: ${describeFiltro(overrideModo)} (propio)` : `Finalizados: ${describeFiltro(modoGlobal)} (general)`}
            onClick={() => setShowFiltroModal(true)}
          >
            <CalendarRange size={18} />
            {overrideModo !== null && <span className="absolute top-2 right-2 h-2 w-2 rounded-full bg-amber-500 ring-2 ring-surface" />}
          </IconButton>
          {can('casos.manage') && (
            <IconButton variant="primary" label={`Añadir caso a ${resumen.flujoNombre}`} onClick={() => setShowNuevoCaso(true)}>
              <Plus size={18} />
            </IconButton>
          )}
        </div>
      </div>

      <div className="px-5 pt-4">
        <ProgresoBarra enCurso={enCurso} finalizados={finalizados} />
      </div>

      <div className="flex flex-1 flex-col gap-0.5 px-2 pt-3 pb-2">
        {tipos.length === 0 ? (
          <p className="px-3 py-2 text-sm text-gray-500">Sin casos en este rango.</p>
        ) : (
          tipos.map((tipo) => (
            <TipoCasoRow key={tipo.tipoCasoId ?? 'sin-tipo'} tipo={tipo} onSelect={(filtro) => onSelectTipo(tipo, filtro, overrideModo ?? modoGlobal)} />
          ))
        )}
      </div>

      <button
        type="button"
        className="group flex items-center justify-between border-t border-gray-100 px-5 py-3 text-sm font-medium text-indigo-600 hover:bg-gray-50"
        onClick={() => navigate(`/casos/lista?flujoId=${resumen.flujoId}`)}
      >
        Ver todos los casos
        <ArrowRight size={16} className="transition-transform group-hover:translate-x-0.5" aria-hidden="true" />
      </button>

      <NuevoCasoModal
        open={showNuevoCaso}
        flujoId={resumen.flujoId}
        flujoNombre={resumen.flujoNombre}
        onClose={() => setShowNuevoCaso(false)}
      />

      <FinalizadosFiltroModal
        open={showFiltroModal}
        title={`Finalizados — ${resumen.flujoNombre}`}
        value={overrideModo ?? modoGlobal}
        onApply={setOverrideModo}
        onClose={() => setShowFiltroModal(false)}
        onUseGlobal={overrideModo !== null ? () => setOverrideModo(null) : undefined}
      />
    </section>
  )
}
