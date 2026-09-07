import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { useState } from 'react'
import { Button } from '../../components/ui/Button'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { ApiError } from '../../lib/apiClient'
import { collapseVariants } from '../../lib/motion/variants'
import { useToast } from '../../lib/toast/useToast'
import { useAuth } from '../auth/useAuth'
import * as casosApi from './api'
import type { EjecucionPasoDto } from './api'
import { CasoEstadoBadge } from './CasoEstadoBadge'

const ESTADOS_REPROCESABLES = ['Completado', 'Fallido', 'Cancelado']

// One RPA transitioning into a second RPA is two Ejecuciones, not one step within the same Ejecucion —
// this lists them all; expanding one shows its own step-by-step progress, scoped to that Ejecucion only.
export function CasoEjecuciones({ casoId, ejecucionActualId }: { casoId: string; ejecucionActualId: string | null }) {
  const [expandedId, setExpandedId] = useState<string | null>(null)

  const query = useQuery({ queryKey: ['caso-ejecuciones', casoId], queryFn: () => casosApi.listEjecuciones(casoId) })

  if (query.isLoading) return <p className="text-sm text-gray-500">Cargando…</p>

  if (query.isError) {
    return (
      <div className="flex items-center justify-between">
        <p className="text-sm text-red-600">No se han podido cargar las ejecuciones.</p>
        <button className="text-sm font-medium text-gray-700 hover:text-gray-900" onClick={() => query.refetch()}>
          Reintentar
        </button>
      </div>
    )
  }

  const ejecuciones = query.data ?? []
  if (ejecuciones.length === 0) return <p className="text-sm text-gray-500">Todavía no hay ejecuciones.</p>

  return (
    <ul className="flex flex-col gap-2">
      {ejecuciones.map((ejecucion, index) => {
        const abierta = expandedId === ejecucion.id
        return (
          <li key={ejecucion.id} className="rounded-md border border-gray-100">
            <button
              type="button"
              className="flex w-full flex-wrap items-center justify-between gap-2 px-3 py-2 text-left text-sm hover:bg-gray-50"
              onClick={() => setExpandedId(abierta ? null : ejecucion.id)}
            >
              <span className="flex items-center gap-3">
                <span className="font-medium text-gray-900">Ejecución {index + 1}</span>
                <CasoEstadoBadge estado={ejecucion.estado} />
                <span className="text-xs text-gray-400">
                  {ejecucion.pasosCount} paso{ejecucion.pasosCount === 1 ? '' : 's'}
                </span>
              </span>
              <span className="text-xs text-gray-400">{new Date(ejecucion.startedAt).toLocaleString()}</span>
            </button>
            <AnimatePresence initial={false}>
              {abierta && (
                <motion.div className="overflow-hidden" variants={collapseVariants} initial="initial" animate="animate" exit="exit">
                  <div className="border-t border-gray-100 px-3 py-3">
                    <EjecucionPasos
                      casoId={casoId}
                      ejecucionId={ejecucion.id}
                      soloLectura={ejecucion.id !== ejecucionActualId}
                    />
                  </div>
                </motion.div>
              )}
            </AnimatePresence>
          </li>
        )
      })}
    </ul>
  )
}

function EjecucionPasos({ casoId, ejecucionId, soloLectura }: { casoId: string; ejecucionId: string; soloLectura: boolean }) {
  const { can } = useAuth()
  const { showToast } = useToast()
  const queryClient = useQueryClient()
  const [pendingReprocesar, setPendingReprocesar] = useState<EjecucionPasoDto | null>(null)

  const query = useQuery({
    queryKey: ['caso-ejecucion', casoId, ejecucionId],
    queryFn: () => casosApi.getEjecucion(casoId, ejecucionId),
  })

  const reprocesar = useMutation({
    mutationFn: (ejecucionPasoId: string) => casosApi.reprocesarPaso(casoId, ejecucionPasoId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['caso-ejecucion', casoId, ejecucionId] })
      queryClient.invalidateQueries({ queryKey: ['caso-ejecuciones', casoId] })
      queryClient.invalidateQueries({ queryKey: ['caso', casoId] })
      setPendingReprocesar(null)
      showToast('success', 'Paso reprocesado.')
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo reprocesar el paso.'),
  })

  if (query.isLoading) return <p className="text-sm text-gray-500">Cargando pasos…</p>
  if (query.isError) return <p className="text-sm text-red-600">No se han podido cargar los pasos de esta ejecución.</p>

  const pasos = query.data?.pasos ?? []
  if (pasos.length === 0) return <p className="text-sm text-gray-500">Sin pasos.</p>

  const esUltimoIntento = (paso: EjecucionPasoDto) =>
    !pasos.some((p) => p.flujoPasoDefId === paso.flujoPasoDefId && p.numeroIntento > paso.numeroIntento)

  function iniciarReprocesar(paso: EjecucionPasoDto) {
    // Retrying a Fallido step is low-stakes (it just didn't work yet) — but reprocessing one that
    // already succeeded or was cancelled can trigger real-world side effects a second time (a robot
    // repeating an action), so those two get an explicit confirmation first.
    if (paso.estado === 'Fallido') {
      reprocesar.mutate(paso.id)
    } else {
      setPendingReprocesar(paso)
    }
  }

  return (
    <ul className="flex flex-col gap-2">
      {pasos.map((paso) => (
        <li key={paso.id} className="flex items-center justify-between rounded-md border border-gray-100 px-3 py-2 text-sm">
          <div className="flex items-center gap-3">
            <CasoEstadoBadge estado={paso.estado} />
            <span className="font-mono text-xs text-gray-500">{paso.tipoPaso}</span>
            {paso.numeroIntento > 1 && <span className="text-xs text-gray-400">intento {paso.numeroIntento}</span>}
          </div>
          <div className="flex items-center gap-3">
            {paso.errorMensaje && <span className="text-xs text-red-600">{paso.errorMensaje}</span>}
            {!soloLectura && can('casos.manage') && ESTADOS_REPROCESABLES.includes(paso.estado) && esUltimoIntento(paso) && (
              <Button variant="ghost" disabled={reprocesar.isPending} onClick={() => iniciarReprocesar(paso)}>
                {reprocesar.isPending ? 'Reprocesando…' : 'Reprocesar'}
              </Button>
            )}
          </div>
        </li>
      ))}

      <ConfirmDialog
        open={pendingReprocesar !== null}
        title="Reprocesar paso"
        message="Este paso ya se dio por resuelto. Reprocesarlo crea un nuevo intento y vuelve a ponerlo en la cola — si lo ejecuta un robot, puede repetir acciones reales. ¿Continuar?"
        confirmLabel="Reprocesar"
        pendingLabel="Reprocesando…"
        pending={reprocesar.isPending}
        onConfirm={() => pendingReprocesar && reprocesar.mutate(pendingReprocesar.id)}
        onCancel={() => setPendingReprocesar(null)}
      />
    </ul>
  )
}
