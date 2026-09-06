import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { useState } from 'react'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { Modal } from '../../components/ui/Modal'
import { Tabs } from '../../components/ui/Tabs'
import { ApiError } from '../../lib/apiClient'
import { duration, ease } from '../../lib/motion/tokens'
import { useToast } from '../../lib/toast/useToast'
import { useAuth } from '../auth/useAuth'
import * as casosApi from './api'
import { CasoEjecuciones } from './CasoEjecuciones'
import { CasoEstadoBadge } from './CasoEstadoBadge'
import { CasoTimelineUnificado } from './CasoTimelineUnificado'

const ESTADOS_ACTIVOS = ['Iniciado', 'EnProgreso', 'Pausado', 'EsperandoRevisionHumana']

type Tab = 'timeline' | 'ejecuciones'

function DatosNegocioIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6" className="h-4 w-4">
      <path d="M9 4c-1.5 0-2 .8-2 2v3c0 1.2-.6 2-1.8 2 1.2 0 1.8.8 1.8 2v3c0 1.2.5 2 2 2" strokeLinecap="round" strokeLinejoin="round" />
      <path d="M15 4c1.5 0 2 .8 2 2v3c0 1.2.6 2 1.8 2-1.2 0-1.8.8-1.8 2v3c0 1.2-.5 2-2 2" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  )
}

/// Standalone screen body for a Caso — header (identity, status, key actions) is visually distinct
/// from the tabbed body below it (Ejecución vs Evidencias, never mixed together), and the business
/// JSON stays out of the way behind an icon button instead of always taking up space on the page.
export function CasoDetailContent({ casoId }: { casoId: string }) {
  const { can } = useAuth()
  const { showToast } = useToast()
  const queryClient = useQueryClient()
  const [tab, setTab] = useState<Tab>('timeline')
  const [showDatos, setShowDatos] = useState(false)
  const [confirmingCancel, setConfirmingCancel] = useState(false)

  const query = useQuery({
    queryKey: ['caso', casoId],
    queryFn: () => casosApi.getCaso(casoId),
    refetchInterval: (q) => (q.state.data?.estado === 'EnProgreso' ? 2000 : false),
  })

  const flujosQuery = useQuery({ queryKey: ['flujos-asignados'], queryFn: casosApi.listFlujosAsignados })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['caso', casoId] })
  const onError = (err: unknown) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo completar la acción.')
  const onSuccessWith = (mensaje: string) => () => {
    invalidate()
    showToast('success', mensaje)
  }

  const pausar = useMutation({ mutationFn: () => casosApi.pausarCaso(casoId), onSuccess: onSuccessWith('Caso pausado.'), onError })
  const reanudar = useMutation({ mutationFn: () => casosApi.reanudarCaso(casoId), onSuccess: onSuccessWith('Caso reanudado.'), onError })
  const cancelar = useMutation({
    mutationFn: () => casosApi.cancelarCaso(casoId),
    onSuccess: () => {
      setConfirmingCancel(false)
      onSuccessWith('Caso cancelado.')()
    },
    onError,
  })
  if (query.isLoading) return <p className="text-gray-500">Cargando…</p>

  if (query.isError) {
    return (
      <Card>
        <div className="flex items-center justify-between">
          <p className="text-sm text-red-600">No se ha podido cargar el caso.</p>
          <button className="text-sm font-medium text-gray-700 hover:text-gray-900" onClick={() => query.refetch()}>
            Reintentar
          </button>
        </div>
      </Card>
    )
  }

  if (!query.data) return <p className="text-gray-500">Caso no encontrado.</p>

  const caso = query.data
  const flujoNombre = flujosQuery.data?.find((f) => f.id === caso.flujoId)?.nombre
  const puedeGestionar = can('casos.manage')

  let datosJsonFormateado: string | null = null
  if (caso.datosJson) {
    try {
      datosJsonFormateado = JSON.stringify(JSON.parse(caso.datosJson), null, 2)
    } catch {
      datosJsonFormateado = caso.datosJson
    }
  }

  return (
    <div className="flex flex-col gap-5">
      <Card>
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <h1 className="text-2xl font-semibold tracking-tight text-gray-900">{caso.titulo}</h1>
            <p className="text-sm text-gray-500">{flujoNombre ?? caso.flujoId}</p>
          </div>
          <div className="flex items-center gap-2">
            {datosJsonFormateado && (
              <button
                type="button"
                title="Ver datos de negocio"
                aria-label="Ver datos de negocio"
                className="inline-flex items-center gap-1.5 rounded-lg border border-gray-300 px-2.5 py-1.5 text-xs font-medium text-gray-600 hover:bg-gray-50"
                onClick={() => setShowDatos(true)}
              >
                <DatosNegocioIcon />
                Datos de negocio
              </button>
            )}
            <CasoEstadoBadge estado={caso.estado} />
          </div>
        </div>

        <div className="mt-4 grid grid-cols-2 gap-4 text-sm sm:grid-cols-4">
          <div>
            <div className="text-gray-500">Estado</div>
            <div className="font-medium text-gray-900">{caso.estadoNegocio?.display ?? 'Sin estado'}</div>
          </div>
          <div>
            <div className="text-gray-500">Creado</div>
            <div className="font-medium text-gray-900">{new Date(caso.createdAt).toLocaleString()}</div>
          </div>
          <div>
            <div className="text-gray-500">Actualizado</div>
            <div className="font-medium text-gray-900">{new Date(caso.updatedAt).toLocaleString()}</div>
          </div>
          <div>
            <div className="text-gray-500">Finalizado</div>
            <div className="font-medium text-gray-900">{caso.completedAt ? new Date(caso.completedAt).toLocaleString() : '—'}</div>
          </div>
        </div>

        {puedeGestionar && (
          <div className="mt-4 flex flex-wrap gap-2 border-t border-gray-100 pt-4">
            {(caso.estado === 'EnProgreso' || caso.estado === 'EsperandoRevisionHumana') && (
              <Button variant="ghost" disabled={pausar.isPending} onClick={() => pausar.mutate()}>
                {pausar.isPending ? 'Pausando…' : 'Pausar'}
              </Button>
            )}
            {caso.estado === 'Pausado' && (
              <Button disabled={reanudar.isPending} onClick={() => reanudar.mutate()}>
                {reanudar.isPending ? 'Reanudando…' : 'Reanudar'}
              </Button>
            )}
            {ESTADOS_ACTIVOS.includes(caso.estado) && (
              <Button variant="danger" disabled={cancelar.isPending} onClick={() => setConfirmingCancel(true)}>
                Cancelar caso
              </Button>
            )}
          </div>
        )}
      </Card>

      <div>
        <Tabs
          tabs={[
            { value: 'timeline', label: 'Timeline' },
            { value: 'ejecuciones', label: 'Ejecuciones' },
          ]}
          active={tab}
          onChange={setTab}
        />

        <AnimatePresence mode="wait">
          {tab === 'timeline' && (
            <motion.div
              key="timeline"
              initial={{ opacity: 0, y: 4 }}
              animate={{ opacity: 1, y: 0, transition: { duration: duration.fast, ease: ease.out } }}
              exit={{ opacity: 0, transition: { duration: duration.fast, ease: ease.in } }}
            >
              <Card className="mt-4">
                <CasoTimelineUnificado casoId={caso.id} />
              </Card>
            </motion.div>
          )}

          {tab === 'ejecuciones' && (
            <motion.div
              key="ejecuciones"
              initial={{ opacity: 0, y: 4 }}
              animate={{ opacity: 1, y: 0, transition: { duration: duration.fast, ease: ease.out } }}
              exit={{ opacity: 0, transition: { duration: duration.fast, ease: ease.in } }}
            >
              <Card className="mt-4">
                <CasoEjecuciones casoId={caso.id} ejecucionActualId={caso.ejecucionActual?.id ?? null} />
              </Card>
            </motion.div>
          )}
        </AnimatePresence>
      </div>

      <Modal open={showDatos && Boolean(datosJsonFormateado)} title="Datos de negocio" onClose={() => setShowDatos(false)}>
        <pre className="overflow-x-auto rounded-md bg-gray-50 p-3 text-xs text-gray-700">{datosJsonFormateado}</pre>
      </Modal>

      <ConfirmDialog
        open={confirmingCancel}
        title="¿Cancelar caso?"
        message="El caso se marcará como cancelado y no se podrá reanudar. Esta acción no se puede deshacer."
        confirmLabel="Cancelar caso"
        pendingLabel="Cancelando…"
        pending={cancelar.isPending}
        onConfirm={() => cancelar.mutate()}
        onCancel={() => setConfirmingCancel(false)}
      />
    </div>
  )
}
