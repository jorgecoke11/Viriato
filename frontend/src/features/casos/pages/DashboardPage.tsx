import { useQuery } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { CalendarRange } from 'lucide-react'
import { useState } from 'react'
import { Button } from '../../../components/ui/Button'
import { Card } from '../../../components/ui/Card'
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

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight text-gray-900">Panel de casos</h1>
          <p className="text-sm text-gray-500">Casos en curso siempre visibles — {describeFiltro(modoGlobal).toLowerCase()}.</p>
        </div>
        <div className="flex items-center gap-2">
          <Button variant="ghost" onClick={() => setShowFiltroModal(true)}>
            <CalendarRange size={16} />
            {describeFiltro(modoGlobal)}
          </Button>
          <Button variant="ghost" onClick={() => setManaging((m) => !m)}>
            {managing ? 'Cerrar' : 'Personalizar cajitas'}
          </Button>
        </div>
      </div>

      <AnimatePresence initial={false}>
        {managing && (
          <motion.div variants={collapseVariants} initial="initial" animate="animate" exit="exit" className="overflow-hidden">
            <Card>
              <h2 className="mb-2 text-sm font-medium text-gray-700">Cajitas visibles</h2>
              {cajitas.length === 0 ? (
                <p className="text-sm text-gray-500">No tienes flujos asignados todavía.</p>
              ) : (
                <div className="flex flex-col gap-2">
                  {cajitas.map((c) => (
                    <label key={c.flujoId} className="flex items-center gap-2 text-sm text-gray-700">
                      <input
                        type="checkbox"
                        className="accent-indigo-600"
                        checked={!hidden.has(c.flujoId)}
                        onChange={() => toggleHidden(c.flujoId)}
                      />
                      {c.flujoNombre}
                    </label>
                  ))}
                </div>
              )}
            </Card>
          </motion.div>
        )}
      </AnimatePresence>

      {query.isLoading && <p className="text-gray-500">Cargando…</p>}

      {query.isError && (
        <Card>
          <div className="flex items-center justify-between">
            <p className="text-sm text-red-600">No se ha podido cargar el panel de casos.</p>
            <button className="text-sm font-medium text-gray-700 hover:text-gray-900" onClick={() => query.refetch()}>
              Reintentar
            </button>
          </div>
        </Card>
      )}

      {query.isSuccess && cajitas.length === 0 && (
        <Card>
          <p className="text-sm text-gray-500">
            No tienes ningún flujo asignado. Pide a un administrador que te asigne uno para empezar a ver casos.
          </p>
        </Card>
      )}

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        {visibles.map((c) => (
          <ProcesoResumenCard
            key={c.flujoId}
            resumen={c}
            modoGlobal={modoGlobal}
            onSelectTipo={(tipo, filtro) =>
              setModalTipo({
                flujoId: c.flujoId,
                flujoNombre: c.flujoNombre,
                tipoCasoId: tipo.tipoCasoId,
                tipoCasoNombre: tipo.nombre,
                filtro,
              })
            }
          />
        ))}
      </div>

      {cajitas.length > 0 && visibles.length === 0 && (
        <p className="text-sm text-gray-500">Todas tus cajitas están ocultas — usa "Personalizar cajitas" para mostrarlas.</p>
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
