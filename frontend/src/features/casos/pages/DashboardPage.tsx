import { useQuery } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { Activity, CalendarRange, CheckCheck, Layers, LayoutGrid, SlidersHorizontal, Workflow } from 'lucide-react'
import { useState } from 'react'
import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
import { EmptyState } from '../../../components/ui/EmptyState'
import { PageHeader } from '../../../components/ui/PageHeader'
import { Skeleton } from '../../../components/ui/Skeleton'
import { StatCard } from '../../../components/ui/StatCard'
import { collapseVariants } from '../../../lib/motion/variants'
import * as casosApi from '../api'
import { getHiddenFlujoIds, setHiddenFlujoIds } from '../dashboardPreferences'
import { describeFiltro, filtroToParams, type FinalizadosFiltro } from '../finalizadosFiltro'
import { FinalizadosFiltroModal } from '../FinalizadosFiltroModal'
import { ProcesoResumenCard, type TipoCasoFiltro } from '../ProcesoResumenCard'
import { TipoCasoModal } from '../TipoCasoModal'

export function DashboardPage() {
  const [modoGlobal, setModoGlobal] = useState<FinalizadosFiltro>({ tipo: 'hoy' })
  const [showFiltroModal, setShowFiltroModal] = useState(false)
  const [hidden, setHidden] = useState(() => getHiddenFlujoIds())
  const [managing, setManaging] = useState(false)
  const [modalTipo, setModalTipo] = useState<{
    flujoId: string
    flujoNombre: string
    tipoCasoId: string | null
    tipoCasoNombre: string
    filtro: TipoCasoFiltro
    modo: FinalizadosFiltro
  } | null>(null)

  const query = useQuery({
    queryKey: ['casos-resumen', modoGlobal],
    queryFn: () => casosApi.getResumen(filtroToParams(modoGlobal)),
  })

  const toggleHidden = (flujoId: string) => {
    const next = new Set(hidden)
    if (next.has(flujoId)) next.delete(flujoId)
    else next.add(flujoId)
    setHidden(next)
    setHiddenFlujoIds(next)
  }

  const cajitas = query.data ?? []
  const visibles = cajitas.filter((c) => !hidden.has(c.flujoId))

  // The headline figures cover exactly what the cards below show: hide a process and it leaves the totals too.
  const totalCasos = visibles.reduce((suma, c) => suma + c.total, 0)
  const enCurso = visibles.reduce((suma, c) => suma + c.porTipo.reduce((s, t) => s + t.enCurso, 0), 0)
  const finalizados = visibles.reduce((suma, c) => suma + c.porTipo.reduce((s, t) => s + t.finalizados, 0), 0)

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Panel de casos"
        description={`Los casos en curso siempre se ven; finalizados: ${describeFiltro(modoGlobal).toLowerCase()}.`}
        actions={
          <>
            <Button variant="secondary" onClick={() => setShowFiltroModal(true)}>
              <CalendarRange size={16} />
              {describeFiltro(modoGlobal)}
            </Button>
            <Button variant={managing ? 'primary' : 'secondary'} aria-pressed={managing} onClick={() => setManaging((m) => !m)}>
              <SlidersHorizontal size={16} />
              {managing ? 'Cerrar' : 'Personalizar'}
            </Button>
          </>
        }
      />

      <AnimatePresence initial={false}>
        {managing && (
          <motion.div variants={collapseVariants} initial="initial" animate="animate" exit="exit" className="overflow-hidden">
            <Card>
              <h2 className="mb-1 text-sm font-semibold text-gray-900">Procesos visibles</h2>
              <p className="mb-3 text-sm text-gray-500">Elige qué procesos aparecen en el panel. Se recuerda en este navegador.</p>
              {cajitas.length === 0 ? (
                <p className="text-sm text-gray-500">No tienes flujos asignados todavía.</p>
              ) : (
                <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                  {cajitas.map((c) => (
                    <label
                      key={c.flujoId}
                      className="flex cursor-pointer items-center gap-3 rounded-lg border border-gray-200 px-3 py-2.5 text-sm text-gray-800 hover:bg-gray-50"
                    >
                      <input
                        type="checkbox"
                        className="h-4 w-4 accent-indigo-600"
                        checked={!hidden.has(c.flujoId)}
                        onChange={() => toggleHidden(c.flujoId)}
                      />
                      <span className="min-w-0 truncate">{c.flujoNombre}</span>
                    </label>
                  ))}
                </div>
              )}
            </Card>
          </motion.div>
        )}
      </AnimatePresence>

      {query.isLoading && (
        <div role="status" aria-label="Cargando el panel" className="flex flex-col gap-6">
          <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
            {Array.from({ length: 4 }, (_, i) => (
              <Skeleton key={i} className="h-[84px] rounded-xl" />
            ))}
          </div>
          <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
            {Array.from({ length: 2 }, (_, i) => (
              <Skeleton key={i} className="h-64 rounded-2xl" />
            ))}
          </div>
        </div>
      )}

      {query.isError && (
        <Card>
          <div className="flex items-center justify-between gap-3">
            <p className="text-sm text-red-600">No se ha podido cargar el panel de casos.</p>
            <Button variant="secondary" size="sm" onClick={() => query.refetch()}>
              Reintentar
            </Button>
          </div>
        </Card>
      )}

      {query.isSuccess && cajitas.length === 0 && (
        <Card className="p-0">
          <EmptyState
            icon={<Workflow size={22} />}
            title="Todavía no tienes procesos"
            description="Pide a un administrador que te asigne un flujo para empezar a ver casos aquí."
          />
        </Card>
      )}

      {query.isSuccess && cajitas.length > 0 && (
        <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
          <StatCard label="Procesos" value={visibles.length} icon={<Workflow size={20} />} tone="brand" hint={`de ${cajitas.length}`} />
          <StatCard label="Casos" value={totalCasos} icon={<Layers size={20} />} tone="neutral" />
          <StatCard label="En curso" value={enCurso} icon={<Activity size={20} />} tone="info" hint="siempre visibles" />
          <StatCard label="Finalizados" value={finalizados} icon={<CheckCheck size={20} />} tone="success" hint={describeFiltro(modoGlobal).toLowerCase()} />
        </div>
      )}

      <div className="grid grid-cols-1 items-start gap-4 xl:grid-cols-2">
        {visibles.map((c) => (
          <ProcesoResumenCard
            key={c.flujoId}
            resumen={c}
            modoGlobal={modoGlobal}
            onSelectTipo={(tipo, filtro, modo) =>
              setModalTipo({
                flujoId: c.flujoId,
                flujoNombre: c.flujoNombre,
                tipoCasoId: tipo.tipoCasoId,
                tipoCasoNombre: tipo.nombre,
                filtro,
                modo,
              })
            }
          />
        ))}
      </div>

      {cajitas.length > 0 && visibles.length === 0 && (
        <Card className="p-0">
          <EmptyState
            icon={<LayoutGrid size={22} />}
            title="Todos los procesos están ocultos"
            description="Usa «Personalizar» para volver a mostrar alguno."
            action={
              <Button variant="secondary" size="sm" onClick={() => setManaging(true)}>
                Personalizar
              </Button>
            }
          />
        </Card>
      )}

      <TipoCasoModal data={modalTipo} onClose={() => setModalTipo(null)} />

      <FinalizadosFiltroModal
        open={showFiltroModal}
        title="Finalizados a mostrar"
        value={modoGlobal}
        onApply={setModoGlobal}
        onClose={() => setShowFiltroModal(false)}
      />
    </div>
  )
}
