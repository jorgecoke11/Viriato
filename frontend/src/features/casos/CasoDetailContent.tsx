import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { Ban, Check, Copy, Hourglass, OctagonAlert, Pause, PauseCircle, Play, RotateCcw, XCircle, type LucideIcon } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { Skeleton } from '../../components/ui/Skeleton'
import { Tabs } from '../../components/ui/Tabs'
import { ApiError } from '../../lib/apiClient'
import { duration, ease } from '../../lib/motion/tokens'
import { useToast } from '../../lib/toast/useToast'
import { useAuth } from '../auth/useAuth'
import * as casosApi from './api'
import type { CasoDetailDto } from './api'
import { BarraDeProgreso } from '../../components/ui/BarraDeProgreso'
import { PanelEnDirecto } from './BotonVerEnDirecto'
import { CancelarEjecucion } from './CancelarEjecucion'
import { CasoDocumentos } from './CasoDocumentos'
import { CasoEjecuciones } from './CasoEjecuciones'
import { CasoEtiquetas } from './CasoEtiquetas'
import { CasoProgreso } from './CasoProgreso'
import { CasoTimelineUnificado } from './CasoTimelineUnificado'
import { DatosDelCasoCard } from './DatosDelCasoCard'
import { duracion, formatFecha, formatFechaCompleta, haceCuanto } from './fechas'
import { PrioridadChip } from './PrioridadChip'
import { pasosResueltos, progresoDelProceso } from './progreso'

const ESTADOS_ACTIVOS = ['Iniciado', 'Pendiente', 'EnProgreso', 'Pausado', 'EsperandoRevisionHumana']

type Tab = 'timeline' | 'ejecuciones' | 'documentos' | 'datos'

type TonoDeAviso = 'danger' | 'info' | 'warning' | 'neutral' | 'success' | 'brand'

const avisos: Record<TonoDeAviso, { caja: string; icono: string }> = {
  danger: { caja: 'border-red-200/70 bg-red-50/60', icono: 'text-red-600' },
  info: { caja: 'border-blue-200/70 bg-blue-50/60', icono: 'text-blue-600' },
  warning: { caja: 'border-amber-200/70 bg-amber-50/60', icono: 'text-amber-600' },
  neutral: { caja: 'border-gray-200 bg-gray-50', icono: 'text-gray-500' },
  success: { caja: 'border-green-200/70 bg-green-50/60', icono: 'text-green-600' },
  brand: { caja: 'border-indigo-200/70 bg-indigo-50/60', icono: 'text-indigo-600' },
}

/** The one thing about the Caso that deserves to be said above everything else: it failed, it waits, it is paused. */
function Aviso({ tono, icono: Icono, titulo, children, accion }: { tono: TonoDeAviso; icono: LucideIcon; titulo: string; children?: ReactNode; accion?: ReactNode }) {
  return (
    <div role="status" className={`flex flex-wrap items-start gap-3 rounded-xl border px-4 py-3 ${avisos[tono].caja}`}>
      <Icono size={20} aria-hidden="true" className={`mt-0.5 shrink-0 ${avisos[tono].icono}`} />
      <div className="min-w-0 flex-1">
        <p className="text-sm font-semibold text-gray-900">{titulo}</p>
        {children && <div className="mt-0.5 text-sm break-words whitespace-pre-wrap text-gray-700">{children}</div>}
      </div>
      {accion && <div className="shrink-0">{accion}</div>}
    </div>
  )
}

function Dato({ etiqueta, children, debajo, titulo }: { etiqueta: string; children: ReactNode; debajo?: string; titulo?: string }) {
  return (
    <div className="bg-surface px-5 py-3.5" title={titulo}>
      <dt className="text-xs font-medium tracking-wide text-gray-500 uppercase">{etiqueta}</dt>
      <dd className="num mt-1 text-sm font-semibold text-gray-900">{children}</dd>
      {debajo && <dd className="num mt-0.5 text-xs text-gray-500">{debajo}</dd>}
    </div>
  )
}

function Fila({ etiqueta, children }: { etiqueta: string; children: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-4 py-2">
      <dt className="shrink-0 text-gray-500">{etiqueta}</dt>
      <dd className="min-w-0 text-right font-medium break-words text-gray-900">{children}</dd>
    </div>
  )
}

/// Standalone screen body for a Caso. From top to bottom: who it is and what it is doing (identity, states, key dates,
/// the actions), the one thing that needs saying (a failure, a wait, a pause), where it is in its process, and then the
/// detail in tabs that each get the full width — its history, runs, documents, and its business data and details.
export function CasoDetailContent({ casoId }: { casoId: string }) {
  const { can } = useAuth()
  const { showToast } = useToast()
  const queryClient = useQueryClient()
  const [tab, setTab] = useState<Tab>('timeline')
  const [confirmingCancel, setConfirmingCancel] = useState(false)
  const [idCopiado, setIdCopiado] = useState(false)
  // The document the history sent us to ("Ver en Documentos"), to be pointed out when that tab opens.
  const [documentoResaltado, setDocumentoResaltado] = useState<string | null>(null)

  const query = useQuery({
    queryKey: ['caso', casoId],
    queryFn: () => casosApi.getCaso(casoId),
    refetchInterval: (q) => (q.state.data?.estado === 'EnProgreso' || q.state.data?.estado === 'Pendiente' ? 2000 : false),
  })

  const flujosQuery = useQuery({ queryKey: ['flujos-asignados'], queryFn: casosApi.listFlujosAsignados })

  // The same queries the history and the documents tab make (they share the cache), here for the counts on the tabs.
  const historialQuery = useQuery({ queryKey: ['caso-timeline', casoId], queryFn: () => casosApi.getTimeline(casoId) })
  const documentosQuery = useQuery({ queryKey: ['caso-documentos', casoId], queryFn: () => casosApi.listDocumentos(casoId) })

  const flujoId = query.data?.flujoId
  const flujoVersionId = query.data?.flujoVersionId
  const versionQuery = useQuery({
    queryKey: ['flujo-version', flujoId, flujoVersionId],
    queryFn: () => casosApi.getFlujoVersion(flujoId!, flujoVersionId!),
    enabled: Boolean(flujoId && flujoVersionId),
    staleTime: 5 * 60_000,
  })

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ['caso', casoId] })
    queryClient.invalidateQueries({ queryKey: ['caso-ejecuciones', casoId] })
    queryClient.invalidateQueries({ queryKey: ['caso-ejecucion', casoId] })
  }
  const onError = (err: unknown) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo completar la acción.')
  const onSuccessWith = (mensaje: string) => () => {
    invalidate()
    showToast('success', mensaje)
  }

  const pausar = useMutation({ mutationFn: () => casosApi.pausarCaso(casoId), onSuccess: onSuccessWith('Caso pausado.'), onError })
  const reanudar = useMutation({ mutationFn: () => casosApi.reanudarCaso(casoId), onSuccess: onSuccessWith('Caso reanudado.'), onError })
  const reprocesar = useMutation({
    mutationFn: (ejecucionPasoId: string) => casosApi.reprocesarPaso(casoId, ejecucionPasoId),
    onSuccess: onSuccessWith('Paso reprocesado.'),
    onError,
  })
  const cancelar = useMutation({
    mutationFn: () => casosApi.cancelarCaso(casoId),
    onSuccess: () => {
      setConfirmingCancel(false)
      onSuccessWith('Caso cancelado.')()
    },
    onError,
  })

  if (query.isLoading) {
    return (
      <div role="status" aria-label="Cargando el caso" className="flex flex-col gap-5">
        <Skeleton className="h-44 rounded-2xl" />
        <Skeleton className="h-28 rounded-xl" />
        <Skeleton className="h-64 rounded-xl" />
      </div>
    )
  }

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

  const caso: CasoDetailDto = query.data
  const flujoNombre = flujosQuery.data?.find((f) => f.id === caso.flujoId)?.nombre
  const puedeGestionar = can('casos.manage')
  const puedeCancelar = can('casos.cancelar')
  const puedePriorizar = can('casos.prioridad')
  const activo = ESTADOS_ACTIVOS.includes(caso.estado)

  const pasosDelCaso = caso.ejecucionActual?.pasos ?? []
  const progreso = progresoDelProceso(versionQuery.data?.pasos ?? [], pasosDelCaso)
  const fallido = progreso.find((p) => p.estado === 'fallido')
  const enCola = progreso.find((p) => p.estado === 'en-cola')
  const enMarcha = progreso.find((p) => p.estado === 'en-curso')
  const cancelado = progreso.find((p) => p.estado === 'cancelado' && p.ultimo?.errorMensaje)

  const terminadoEn = caso.completedAt
  const duracionTotal = duracion((terminadoEn ? new Date(terminadoEn) : new Date()).getTime() - new Date(caso.createdAt).getTime())

  async function copiarId() {
    try {
      await navigator.clipboard.writeText(caso.id)
      setIdCopiado(true)
      setTimeout(() => setIdCopiado(false), 1500)
    } catch {
      showToast('error', 'No se pudo copiar.')
    }
  }

  // `enCaja`: the content sits on a card of its own; the history and the data bring their own surfaces.
  const panel = (clave: Tab, contenido: ReactNode, enCaja = true) => (
    <motion.div
      key={clave}
      className="mt-5"
      initial={{ opacity: 0, y: 4 }}
      animate={{ opacity: 1, y: 0, transition: { duration: duration.fast, ease: ease.out } }}
      exit={{ opacity: 0, transition: { duration: duration.fast, ease: ease.in } }}
    >
      {enCaja ? <Card>{contenido}</Card> : contenido}
    </motion.div>
  )

  function verDocumento(documentoId: string) {
    setDocumentoResaltado(documentoId)
    setTab('documentos')
  }

  return (
    <div className="flex flex-col gap-5">
      <section className="overflow-hidden rounded-2xl border border-gray-200 bg-surface shadow-card">
        <div className="flex flex-wrap items-start justify-between gap-x-6 gap-y-4 px-6 pt-6 pb-5">
          <div className="min-w-0 flex-1">
            <div className="mb-4">
              <CasoEtiquetas
                estado={caso.estado}
                estadoNegocio={caso.estadoNegocio?.display ?? null}
                estadoNegocioFinal={caso.estadoNegocio?.esFinal}
                tipoCaso={caso.tipoCaso}
              />
            </div>
            <h1 className="page-title break-words">{caso.titulo}</h1>
            <p className="mt-1 text-sm text-gray-500">
              {flujoNombre ?? 'Proceso'}
              {versionQuery.data && <span> · versión {versionQuery.data.numeroVersion}</span>}
            </p>
          </div>

          {(puedeGestionar || puedeCancelar) && activo && (
            <div className="flex flex-wrap items-center gap-2">
              {puedeGestionar && (caso.estado === 'EnProgreso' || caso.estado === 'Pendiente' || caso.estado === 'EsperandoRevisionHumana') && (
                <Button variant="secondary" disabled={pausar.isPending} onClick={() => pausar.mutate()}>
                  <Pause size={15} aria-hidden="true" />
                  {pausar.isPending ? 'Pausando…' : 'Pausar'}
                </Button>
              )}
              {puedeGestionar && caso.estado === 'Pausado' && (
                <Button disabled={reanudar.isPending} onClick={() => reanudar.mutate()}>
                  <Play size={15} aria-hidden="true" />
                  {reanudar.isPending ? 'Reanudando…' : 'Reanudar'}
                </Button>
              )}
              {puedeCancelar && (
                <Button variant="danger" disabled={cancelar.isPending} onClick={() => setConfirmingCancel(true)}>
                  <Ban size={15} aria-hidden="true" />
                  Cancelar caso
                </Button>
              )}
            </div>
          )}
        </div>

        <dl className="grid grid-cols-2 gap-px border-t border-gray-200 bg-gray-200 lg:grid-cols-4">
          <Dato etiqueta="Creado" debajo={haceCuanto(caso.createdAt)} titulo={formatFechaCompleta(caso.createdAt)}>
            {formatFecha(caso.createdAt)}
          </Dato>
          {terminadoEn ? (
            <Dato etiqueta="Finalizado" debajo={haceCuanto(terminadoEn)} titulo={formatFechaCompleta(terminadoEn)}>
              {formatFecha(terminadoEn)}
            </Dato>
          ) : (
            <Dato etiqueta="Última actividad" debajo={haceCuanto(caso.updatedAt)} titulo={formatFechaCompleta(caso.updatedAt)}>
              {formatFecha(caso.updatedAt)}
            </Dato>
          )}
          <Dato etiqueta="Duración" debajo={terminadoEn ? 'de principio a fin' : 'y contando'}>
            {duracionTotal}
          </Dato>
          <Dato etiqueta="Pasos" debajo={progreso.length > 0 ? 'resueltos' : undefined}>
            {progreso.length > 0 ? `${pasosResueltos(progreso)} de ${progreso.length}` : '—'}
          </Dato>
        </dl>
      </section>

      {caso.estado === 'Fallido' && fallido && (
        <Aviso
          tono="danger"
          icono={OctagonAlert}
          titulo={`El paso «${fallido.nombre}» falló`}
          accion={
            puedeGestionar &&
            fallido.ultimo && (
              <Button variant="secondary" size="sm" disabled={reprocesar.isPending} onClick={() => reprocesar.mutate(fallido.ultimo!.id)}>
                <RotateCcw size={13} aria-hidden="true" />
                {reprocesar.isPending ? 'Reprocesando…' : 'Reprocesar'}
              </Button>
            )
          }
        >
          {fallido.ultimo?.errorMensaje ?? 'No se indicó el motivo.'}
        </Aviso>
      )}

      {caso.estado === 'Cancelado' && (
        <Aviso tono="neutral" icono={XCircle} titulo="Este caso se canceló">
          {cancelado?.ultimo?.errorMensaje ?? 'Ya no seguirá ejecutándose.'}
        </Aviso>
      )}

      {caso.estado === 'Pausado' && (
        <Aviso tono="warning" icono={PauseCircle} titulo="El caso está en pausa">
          No se ejecutará ningún paso más hasta que lo reanudes.
        </Aviso>
      )}

      {caso.estado === 'EnProgreso' && enMarcha?.ultimo && (
        <Aviso
          tono="brand"
          icono={Play}
          titulo={`«${enMarcha.nombre}» se está ejecutando`}
          accion={puedeCancelar && <CancelarEjecucion casoId={caso.id} ejecucionPasoId={enMarcha.ultimo.id} nombre={caso.titulo} />}
        >
          {enMarcha.ultimo.startedAt ? `Un robot lo tiene en marcha desde ${formatFecha(enMarcha.ultimo.startedAt)} (${haceCuanto(enMarcha.ultimo.startedAt)}).` : 'Un robot lo tiene en marcha.'}
          {/* What the robot says about how it is going: the phase and how far along. */}
          {(enMarcha.ultimo.progresoMensaje || enMarcha.ultimo.progresoPorcentaje != null) && (
            <span className="mt-2 flex flex-col gap-1.5">
              {enMarcha.ultimo.progresoMensaje && <span className="font-medium text-gray-800">{enMarcha.ultimo.progresoMensaje}</span>}
              {enMarcha.ultimo.progresoPorcentaje != null && (
                <BarraDeProgreso porcentaje={enMarcha.ultimo.progresoPorcentaje} etiqueta="Avance del paso en marcha" className="max-w-md" />
              )}
            </span>
          )}
        </Aviso>
      )}

      {/* A robot is running it and said where its screen is: anyone who can see the case can watch it, right here. */}
      {caso.estado === 'EnProgreso' && <PanelEnDirecto url={enMarcha?.ultimo?.vistaEnDirectoUrl} titulo={caso.titulo} />}

      {caso.estado === 'Pendiente' && enCola?.ultimo && (
        <Aviso
          tono="info"
          icono={Hourglass}
          titulo={`«${enCola.nombre}» espera a un robot`}
          accion={
            <span className="flex flex-wrap items-center justify-end gap-2">
              {enCola.ultimo.prioridad !== null && (
                <PrioridadChip casoId={caso.id} ejecucionPasoId={enCola.ultimo.id} prioridad={enCola.ultimo.prioridad} puedeEditar={puedePriorizar} />
              )}
              {puedeCancelar && <CancelarEjecucion casoId={caso.id} ejecucionPasoId={enCola.ultimo.id} nombre={caso.titulo} />}
            </span>
          }
        >
          {enCola.ultimo.startedAt ? `En la cola ${haceCuanto(enCola.ultimo.startedAt)}.` : 'En la cola.'}
        </Aviso>
      )}

      <Card>
        <h2 className="mb-4 text-sm font-semibold text-gray-900">Progreso del proceso</h2>
        {versionQuery.isLoading ? <Skeleton className="h-16" /> : <CasoProgreso pasos={progreso} />}
      </Card>

      <div className="min-w-0">
        <Tabs
          tabs={[
            { value: 'timeline', label: 'Historial', cantidad: historialQuery.data?.length },
            { value: 'ejecuciones', label: 'Ejecuciones' },
            { value: 'documentos', label: 'Documentos', cantidad: documentosQuery.data?.length },
            { value: 'datos', label: 'Datos del caso' },
          ]}
          active={tab}
          onChange={setTab}
        />

        <AnimatePresence mode="wait">
          {tab === 'timeline' && panel('timeline', <CasoTimelineUnificado casoId={caso.id} alVerDocumento={verDocumento} />, false)}
          {tab === 'ejecuciones' &&
            panel(
              'ejecuciones',
              <CasoEjecuciones
                casoId={caso.id}
                flujoId={caso.flujoId}
                flujoVersionId={caso.flujoVersionId}
                ejecucionActualId={caso.ejecucionActual?.id ?? null}
              />,
              false,
            )}
          {tab === 'documentos' && panel('documentos', <CasoDocumentos casoId={caso.id} resaltarId={documentoResaltado} />)}
          {tab === 'datos' &&
            panel(
              'datos',
              <div className="grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_22rem]">
                <DatosDelCasoCard datosJson={caso.datosJson} />

                <Card className="p-0">
                  <h2 className="border-b border-gray-100 px-5 py-3 text-sm font-semibold text-gray-900">Detalles</h2>
                  <dl className="divide-y divide-gray-100 px-5 py-1 text-sm">
                    <Fila etiqueta="Proceso">{flujoNombre ?? '—'}</Fila>
                    {versionQuery.data && <Fila etiqueta="Versión">{versionQuery.data.numeroVersion}</Fila>}
                    <Fila etiqueta="Tipo de caso">{caso.tipoCaso ?? 'Sin tipo'}</Fila>
                    <Fila etiqueta="Identificador">
                      <button
                        type="button"
                        title="Copiar el identificador completo"
                        className="inline-flex items-center gap-1.5 rounded-md px-1.5 py-0.5 font-mono text-xs text-gray-700 hover:bg-gray-100"
                        onClick={copiarId}
                      >
                        {caso.id.slice(0, 8)}
                        {idCopiado ? <Check size={12} aria-hidden="true" /> : <Copy size={12} aria-hidden="true" />}
                      </button>
                    </Fila>
                  </dl>
                </Card>
              </div>,
              false,
            )}
        </AnimatePresence>
      </div>

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
