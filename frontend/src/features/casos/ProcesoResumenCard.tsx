import { useQuery } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { CalendarRange, ChevronDown, Plus, Workflow } from 'lucide-react'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { spring } from '../../lib/motion/tokens'
import { collapseVariants } from '../../lib/motion/variants'
import * as casosApi from './api'
import type { EstadoConteoDto, TipoCasoConteoDto, FlujoResumenDto } from './api'
import { describeFiltro, filtroToParams, type FinalizadosFiltro } from './finalizadosFiltro'
import { FinalizadosFiltroModal } from './FinalizadosFiltroModal'
import { NuevoCasoModal } from './NuevoCasoModal'

export type TipoCasoFiltro =
  | { kind: 'bucket'; bucket: 'en-curso' | 'finalizado' }
  | { kind: 'estado'; codigo: string | null; display: string }

// One consistent rule for the whole hierarchy: finalized = blue (it's "settled", it joins the same
// blue the process header is branded in), still-moving = white/neutral (it hasn't earned that color
// yet). Applied identically to the summary chips and to each expanded estado row.
function ConteoChip({ count, variant, onClick }: { count: number; variant: 'en-curso' | 'finalizado'; onClick: () => void }) {
  return (
    <button
      type="button"
      disabled={count === 0}
      title={variant === 'en-curso' ? 'En curso' : 'Finalizados'}
      aria-label={`${variant === 'en-curso' ? 'En curso' : 'Finalizados'}: ${count}`}
      className={`min-w-[1.75rem] rounded-full px-2 py-1 text-center text-xs font-semibold transition-colors disabled:opacity-40 ${
        variant === 'en-curso'
          ? 'border border-blue-200 bg-white text-blue-700 enabled:hover:bg-blue-50'
          : 'bg-blue-600 text-white enabled:hover:bg-blue-700'
      }`}
      onClick={(e) => {
        e.stopPropagation()
        onClick()
      }}
    >
      {count}
    </button>
  )
}

function TipoCasoAccordion({
  tipo,
  onSelect,
}: {
  tipo: TipoCasoConteoDto
  onSelect: (filtro: TipoCasoFiltro) => void
}) {
  const [open, setOpen] = useState(false)
  const porEstado = [...tipo.porEstado].sort((a, b) => a.orden - b.orden)

  return (
    <div className="overflow-hidden rounded-xl border border-blue-100 border-l-4 border-l-blue-500 bg-blue-50/70">
      <div className="flex w-full items-center justify-between gap-3 px-4 py-3">
        <button
          type="button"
          className="flex min-w-0 flex-1 items-center text-left"
          onClick={() => setOpen((o) => !o)}
          aria-expanded={open}
        >
          <span className="min-w-0 truncate text-base font-bold text-blue-950">{tipo.nombre}</span>
        </button>
        <span className="flex shrink-0 items-center gap-1.5">
          <ConteoChip count={tipo.enCurso} variant="en-curso" onClick={() => onSelect({ kind: 'bucket', bucket: 'en-curso' })} />
          <ConteoChip count={tipo.finalizados} variant="finalizado" onClick={() => onSelect({ kind: 'bucket', bucket: 'finalizado' })} />
          <button
            type="button"
            aria-label={open ? 'Contraer' : 'Expandir'}
            aria-expanded={open}
            className="rounded p-0.5 text-blue-400 hover:text-blue-700"
            onClick={() => setOpen((o) => !o)}
          >
            <motion.span className="block" animate={{ rotate: open ? 180 : 0 }} transition={spring.snappy}>
              <ChevronDown size={16} />
            </motion.span>
          </button>
        </span>
      </div>

      <AnimatePresence initial={false}>
        {open && (
          <motion.div
            className="overflow-hidden"
            variants={collapseVariants}
            initial="initial"
            animate="animate"
            exit="exit"
          >
            <div className="flex flex-col gap-1.5 border-t border-blue-100 px-4 py-3">
              {porEstado.length === 0 ? (
                <p className="text-sm text-gray-500">Sin casos.</p>
              ) : (
                porEstado.map((estado: EstadoConteoDto) => (
                  <button
                    key={estado.codigo ?? 'sin-estado'}
                    type="button"
                    className={`flex items-center justify-between rounded-lg px-2.5 py-1.5 text-left text-sm font-medium transition-colors ${
                      estado.esFinal
                        ? 'bg-blue-600 text-white hover:bg-blue-700'
                        : 'border border-gray-200 bg-white text-gray-700 hover:bg-gray-50'
                    }`}
                    onClick={() => onSelect({ kind: 'estado', codigo: estado.codigo, display: estado.display })}
                  >
                    <span>{estado.display}</span>
                    <span className="font-semibold">{estado.count}</span>
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

// The client-facing read on a process: identity (name, total) lives on a branded gradient header —
// but only the header, so the gradient reads as "this is the process" rather than bleeding into
// everything inside it. Below it, a plain white body holds one accordion per Tipo de caso, each with
// its own lighter, still-blue-but-distinct treatment — a subtler echo of the header, not a repeat of it.
//
// Actions that trigger something *for this specific process* (today: starting a new Caso) live as
// icon buttons in this same header, next to the identity block — never as a standalone item in the
// sidebar. "Proceso -> its actions", not "menu -> a global action that happens to need a process
// picker". The trailing icon-button row is exactly where the next such action would go too.
export function ProcesoResumenCard({
  resumen,
  modoGlobal,
  onSelectTipo,
}: {
  resumen: FlujoResumenDto
  modoGlobal: FinalizadosFiltro
  onSelectTipo: (tipo: TipoCasoConteoDto, filtro: TipoCasoFiltro) => void
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

  return (
    <div className="overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-sm">
      <div className="flex items-center gap-3 bg-gradient-to-br from-blue-600 via-blue-700 to-indigo-950 px-6 py-5">
        <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-white/15 text-white">
          <Workflow size={22} />
        </span>
        <div className="min-w-0 flex-1">
          <h2 className="truncate text-lg font-semibold text-white">{resumen.flujoNombre}</h2>
          <p className="text-sm text-blue-100">
            {activo.total} {activo.total === 1 ? 'caso' : 'casos'}
          </p>
        </div>
        <div className="flex shrink-0 items-center gap-1.5">
          <button
            type="button"
            title={overrideModo ? `Finalizados: ${describeFiltro(overrideModo)} (propio)` : `Finalizados: ${describeFiltro(modoGlobal)} (general)`}
            aria-label="Filtrar finalizados de este proceso"
            onClick={() => setShowFiltroModal(true)}
            className="relative flex h-9 w-9 items-center justify-center rounded-lg bg-white/15 text-white transition-colors hover:bg-white/25"
          >
            <CalendarRange size={18} />
            {overrideModo !== null && (
              <span className="absolute right-1 top-1 h-1.5 w-1.5 rounded-full bg-amber-300" />
            )}
          </button>
          {can('casos.manage') && (
            <button
              type="button"
              title="Añadir caso"
              aria-label={`Añadir caso a ${resumen.flujoNombre}`}
              onClick={() => setShowNuevoCaso(true)}
              className="flex h-9 w-9 items-center justify-center rounded-lg bg-white/15 text-white transition-colors hover:bg-white/25"
            >
              <Plus size={18} />
            </button>
          )}
        </div>
      </div>

      <div className="flex flex-col gap-4 p-6">
        {tipos.length === 0 ? (
          <p className="text-sm text-gray-500">Sin casos en este rango.</p>
        ) : (
          <div className="flex flex-col gap-2.5">
            {tipos.map((tipo) => (
              <TipoCasoAccordion
                key={tipo.tipoCasoId ?? 'sin-tipo'}
                tipo={tipo}
                onSelect={(filtro) => onSelectTipo(tipo, filtro)}
              />
            ))}
          </div>
        )}

        <button
          type="button"
          className="self-start text-sm font-medium text-blue-700 hover:text-blue-800 hover:underline"
          onClick={() => navigate(`/casos/lista?flujoId=${resumen.flujoId}`)}
        >
          Ver todos los casos →
        </button>
      </div>

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
    </div>
  )
}
