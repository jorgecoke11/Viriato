import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { Ban, Check, ChevronRight, CircleCheck, CircleX, Clock, Eye, Layers, Loader2, Monitor, Pause, RotateCcw, SkipForward, Workflow, X, type LucideIcon } from 'lucide-react'
import { useState } from 'react'
import { Badge } from '../../components/ui/Badge'
import { Button } from '../../components/ui/Button'
import { ConfirmDialog } from '../../components/ui/ConfirmDialog'
import { EmptyState } from '../../components/ui/EmptyState'
import { Skeleton } from '../../components/ui/Skeleton'
import { Timeline, TimelineGrupo, TimelineItem, type TonoDeNodo } from '../../components/ui/Timeline'
import { ApiError } from '../../lib/apiClient'
import { spring } from '../../lib/motion/tokens'
import { collapseVariants } from '../../lib/motion/variants'
import { useToast } from '../../lib/toast/useToast'
import { useAuth } from '../auth/useAuth'
import * as casosApi from './api'
import type { EjecucionPasoDto, EjecucionResumenDto } from './api'
import { CancelarEjecucion } from './CancelarEjecucion'
import { CasoEstadoBadge } from './CasoEstadoBadge'
import { CasoProgreso } from './CasoProgreso'
import { duracion, formatFecha, formatFechaCompleta, haceCuanto } from './fechas'
import { PrioridadChip } from './PrioridadChip'
import { pasosResueltos, progresoDelProceso } from './progreso'

const ESTADOS_REPROCESABLES = ['Completado', 'Fallido', 'Cancelado']

// How each state of a step looks on the rail: its icon and the colour of the node. The colour never works alone: every step also
// carries its state as a word next to its name.
const estadosDelPaso: Record<string, { icono: LucideIcon; tono: TonoDeNodo; girando?: boolean }> = {
  Completado: { icono: Check, tono: 'green' },
  Fallido: { icono: X, tono: 'red' },
  Cancelado: { icono: Ban, tono: 'gray' },
  Omitido: { icono: SkipForward, tono: 'gray' },
  Pendiente: { icono: Clock, tono: 'gray' },
  EnProgreso: { icono: Loader2, tono: 'blue', girando: true },
  EsperandoRevisionHumana: { icono: Eye, tono: 'purple' },
}

// The same for a whole run, in the tile that leads its card.
const estadosDeLaEjecucion: Record<string, { icono: LucideIcon; caja: string; girando?: boolean }> = {
  Completada: { icono: CircleCheck, caja: 'bg-green-100 text-green-600' },
  Fallida: { icono: CircleX, caja: 'bg-red-100 text-red-600' },
  Cancelada: { icono: Ban, caja: 'bg-gray-100 text-gray-500' },
  EnProgreso: { icono: Loader2, caja: 'bg-blue-100 text-blue-600', girando: true },
  Pausada: { icono: Pause, caja: 'bg-amber-100 text-amber-600' },
}

const TIPOS_DE_PASO: Record<string, string> = {
  Rpa: 'RPA',
  Agente: 'Agente',
  Api: 'API',
  Interno: 'Interno',
  Decision: 'Decisión',
  Espera: 'Espera',
  RevisionHumana: 'Revisión humana',
}

/** How long a run took, or has been going: "3 min", and the word that says which. */
function tiempoDeLaEjecucion(ejecucion: EjecucionResumenDto): { valor: string; nota: string } {
  const fin = ejecucion.finishedAt ? new Date(ejecucion.finishedAt).getTime() : Date.now()
  return { valor: duracion(fin - new Date(ejecucion.startedAt).getTime()), nota: ejecucion.finishedAt ? 'de principio a fin' : 'y contando' }
}

function Dato({ etiqueta, valor, nota }: { etiqueta: string; valor: string; nota?: string }) {
  return (
    <div className="min-w-0">
      <dt className="text-[11px] font-medium tracking-wide text-gray-500 uppercase">{etiqueta}</dt>
      <dd className="num mt-0.5 text-sm font-semibold text-gray-900">{valor}</dd>
      {nota && <dd className="num text-[11px] text-gray-500">{nota}</dd>}
    </div>
  )
}

/**
 * The runs of the process for a Caso, newest first, each as a card: how it ended (or that it is going), when it started, how long
 * it took. Opening one shows where the Caso stood in the process during it and, step by step, what happened: which service and
 * machine ran it, how long, and why it failed. The run in progress is open when the tab is.
 */
export function CasoEjecuciones({
  casoId,
  flujoId,
  flujoVersionId,
  ejecucionActualId,
}: {
  casoId: string
  flujoId: string
  flujoVersionId: string
  ejecucionActualId: string | null
}) {
  // The run in progress is the one anyone opening this tab came for.
  const [expandedId, setExpandedId] = useState<string | null>(ejecucionActualId)

  const query = useQuery({ queryKey: ['caso-ejecuciones', casoId], queryFn: () => casosApi.listEjecuciones(casoId) })

  if (query.isLoading) {
    return (
      <div role="status" aria-label="Cargando las ejecuciones" className="flex flex-col gap-3">
        <Skeleton className="h-20 rounded-xl" />
        <Skeleton className="h-20 rounded-xl" />
      </div>
    )
  }

  if (query.isError) {
    return (
      <div className="flex items-center justify-between rounded-xl border border-gray-200 bg-surface p-4">
        <p className="text-sm text-red-600">No se han podido cargar las ejecuciones.</p>
        <Button variant="secondary" size="sm" onClick={() => query.refetch()}>
          Reintentar
        </Button>
      </div>
    )
  }

  // Oldest first from the server; people want the latest on top, but keep the number it had (Ejecución 1 is the first one).
  const ejecuciones = (query.data ?? []).map((e, i) => ({ ejecucion: e, numero: i + 1 })).reverse()
  if (ejecuciones.length === 0) {
    return (
      <div className="rounded-xl border border-gray-200 bg-surface">
        <EmptyState icon={<Layers size={22} />} title="Todavía no hay ejecuciones" description="Cada vez que el caso recorre su proceso queda aquí una ejecución, con sus pasos." />
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm text-gray-500">
        <span className="num font-medium text-gray-800">{ejecuciones.length}</span> {ejecuciones.length === 1 ? 'ejecución' : 'ejecuciones'} del proceso, la última arriba.
      </p>

      <ul className="flex flex-col gap-3">
        {ejecuciones.map(({ ejecucion, numero }) => {
          const abierta = expandedId === ejecucion.id
          const actual = ejecucion.id === ejecucionActualId
          const { icono: Icono, caja, girando } = estadosDeLaEjecucion[ejecucion.estado] ?? { icono: Workflow, caja: 'bg-gray-100 text-gray-500' }
          const tiempo = tiempoDeLaEjecucion(ejecucion)

          return (
            <li key={ejecucion.id} className="overflow-hidden rounded-xl border border-gray-200 bg-surface shadow-sm">
              <button
                type="button"
                aria-expanded={abierta}
                className="flex w-full flex-wrap items-center gap-x-4 gap-y-3 px-4 py-3.5 text-left hover:bg-gray-50"
                onClick={() => setExpandedId(abierta ? null : ejecucion.id)}
              >
                <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${caja}`}>
                  <Icono size={20} aria-hidden="true" className={girando ? 'motion-safe:animate-spin' : ''} />
                </span>

                <span className="min-w-0 flex-1 basis-48">
                  <span className="flex flex-wrap items-center gap-2">
                    <span className="text-sm font-semibold text-gray-900">Ejecución {numero}</span>
                    <CasoEstadoBadge estado={ejecucion.estado} />
                    {actual && (
                      <Badge tone="brand" dot={false}>
                        Actual
                      </Badge>
                    )}
                  </span>
                  <span className="num mt-0.5 block text-xs text-gray-500" title={formatFechaCompleta(ejecucion.startedAt)}>
                    Empezó {formatFecha(ejecucion.startedAt)} · {haceCuanto(ejecucion.startedAt)}
                  </span>
                </span>

                <dl className="flex shrink-0 items-start gap-6">
                  <Dato etiqueta="Duración" valor={tiempo.valor} nota={tiempo.nota} />
                  <Dato etiqueta="Pasos" valor={String(ejecucion.pasosCount)} nota={ejecucion.pasosCount === 1 ? 'registrado' : 'registrados'} />
                </dl>

                <motion.span className="shrink-0 text-gray-400" animate={{ rotate: abierta ? 90 : 0 }} transition={spring.snappy}>
                  <ChevronRight size={18} aria-hidden="true" />
                </motion.span>
              </button>

              <AnimatePresence initial={false}>
                {abierta && (
                  <motion.div className="overflow-hidden" variants={collapseVariants} initial="initial" animate="animate" exit="exit">
                    <div className="border-t border-gray-100 bg-gray-50/50 px-4 py-5">
                      <EjecucionPasos
                        casoId={casoId}
                        flujoId={flujoId}
                        flujoVersionId={flujoVersionId}
                        ejecucionId={ejecucion.id}
                        soloLectura={!actual}
                      />
                    </div>
                  </motion.div>
                )}
              </AnimatePresence>
            </li>
          )
        })}
      </ul>
    </div>
  )
}

/** What a step is doing, said the way a person would: when it started and how long it took, or since when it waits. */
function lineaDeTiempo(paso: EjecucionPasoDto): string {
  if (paso.enCola) return paso.startedAt ? `Esperando a un robot · ${haceCuanto(paso.startedAt)}` : 'Esperando a un robot'
  if (paso.estado === 'EnProgreso' && paso.startedAt) return `En marcha · ${haceCuanto(paso.startedAt)}`
  if (paso.startedAt && paso.finishedAt) {
    const ms = new Date(paso.finishedAt).getTime() - new Date(paso.startedAt).getTime()
    return `${formatFecha(paso.startedAt)} · duró ${duracion(ms)}`
  }
  if (paso.finishedAt) return formatFecha(paso.finishedAt)
  return ''
}

function EjecucionPasos({
  casoId,
  flujoId,
  flujoVersionId,
  ejecucionId,
  soloLectura,
}: {
  casoId: string
  flujoId: string
  flujoVersionId: string
  ejecucionId: string
  soloLectura: boolean
}) {
  const { can } = useAuth()
  const { showToast } = useToast()
  const queryClient = useQueryClient()
  const [pendingReprocesar, setPendingReprocesar] = useState<EjecucionPasoDto | null>(null)
  const puedeGestionar = can('casos.manage')
  const puedeCancelar = can('casos.cancelar')
  const puedePriorizar = can('casos.prioridad')

  const query = useQuery({
    queryKey: ['caso-ejecucion', casoId, ejecucionId],
    queryFn: () => casosApi.getEjecucion(casoId, ejecucionId),
    // A step that waits or runs changes under our feet: keep what is shown close to what is happening.
    refetchInterval: (q) => (q.state.data?.pasos.some((p) => p.estado === 'EnProgreso') ? 4000 : false),
  })

  // The names of the steps live in the version of the process the Caso runs on; without them a step is only its type.
  const version = useQuery({
    queryKey: ['flujo-version', flujoId, flujoVersionId],
    queryFn: () => casosApi.getFlujoVersion(flujoId, flujoVersionId),
    staleTime: 5 * 60_000,
  })
  const nombres = new Map(version.data?.pasos.map((p) => [p.id, p.nombre]))

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

  if (query.isLoading) return <Skeleton className="h-24 rounded-xl" />
  if (query.isError) return <p className="text-sm text-red-600">No se han podido cargar los pasos de esta ejecución.</p>

  const pasos = query.data?.pasos ?? []
  if (pasos.length === 0) return <p className="text-sm text-gray-500">Sin pasos.</p>

  const progreso = progresoDelProceso(version.data?.pasos ?? [], pasos)

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
    <div className="flex flex-col gap-5">
      {progreso.length > 0 && (
        <section aria-label="Dónde estaba el caso en su proceso" className="rounded-xl border border-gray-200 bg-surface px-4 py-4">
          <h4 className="mb-3 flex items-baseline gap-2 text-xs font-semibold tracking-wide text-gray-500 uppercase">
            Proceso
            <span className="num font-mono font-medium normal-case">
              {pasosResueltos(progreso)} de {progreso.length} resueltos
            </span>
          </h4>
          <CasoProgreso pasos={progreso} />
        </section>
      )}

      <Timeline aria-label="Pasos de la ejecución">
        <TimelineGrupo etiqueta="Pasos" detalle={`${pasos.length} ${pasos.length === 1 ? 'intento' : 'intentos'} registrados`}>
          {pasos.map((paso) => {
            const { icono, tono, girando } = estadosDelPaso[paso.estado] ?? { icono: Clock, tono: 'gray' as const }
            const nombre = nombres.get(paso.flujoPasoDefId) ?? TIPOS_DE_PASO[paso.tipoPaso] ?? paso.tipoPaso
            const tiempo = lineaDeTiempo(paso)
            const reprocesable = !soloLectura && puedeGestionar && ESTADOS_REPROCESABLES.includes(paso.estado) && esUltimoIntento(paso)

            return (
              <TimelineItem key={paso.id} icono={icono} tono={tono} girando={girando && !paso.enCola}>
                <div className="rounded-xl border border-gray-200 bg-surface p-4 shadow-sm">
                  <div className="flex flex-wrap items-start justify-between gap-x-3 gap-y-2">
                    <div className="min-w-0">
                      <p className="font-medium break-words text-gray-900">{nombre}</p>
                      <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
                        <CasoEstadoBadge estado={paso.enCola ? 'Pendiente' : paso.estado} />
                        <Badge tone="neutral" dot={false}>
                          {TIPOS_DE_PASO[paso.tipoPaso] ?? paso.tipoPaso}
                        </Badge>
                        {paso.numeroIntento > 1 && (
                          <Badge tone="warning" dot={false}>
                            Intento {paso.numeroIntento}
                          </Badge>
                        )}
                      </div>
                    </div>

                    <span className="flex flex-wrap items-center gap-2">
                      {paso.enCola && paso.prioridad !== null && (
                        <PrioridadChip casoId={casoId} ejecucionPasoId={paso.id} prioridad={paso.prioridad} puedeEditar={puedePriorizar} />
                      )}
                      {puedeCancelar && paso.estado === 'EnProgreso' && paso.prioridad !== null && (
                        <CancelarEjecucion casoId={casoId} ejecucionPasoId={paso.id} nombre={nombre} />
                      )}
                      {reprocesable && (
                        <Button variant="secondary" size="sm" disabled={reprocesar.isPending} onClick={() => iniciarReprocesar(paso)}>
                          <RotateCcw size={13} aria-hidden="true" />
                          {reprocesar.isPending ? 'Reprocesando…' : 'Reprocesar'}
                        </Button>
                      )}
                    </span>
                  </div>

                  {(tiempo || paso.servicioNombre) && (
                    <p className="num mt-2.5 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-gray-500">
                      {tiempo && (
                        <span className="inline-flex items-center gap-1.5" title={paso.startedAt ? formatFechaCompleta(paso.startedAt) : undefined}>
                          <Clock size={12} aria-hidden="true" />
                          {tiempo}
                        </span>
                      )}
                      {paso.servicioNombre && (
                        <span className="inline-flex items-center gap-1.5" title="El servicio cuyos robots ejecutan este paso">
                          <Workflow size={12} aria-hidden="true" />
                          {paso.servicioNombre}
                        </span>
                      )}
                      {paso.equipoNombre && (
                        <span className="inline-flex items-center gap-1.5" title="La máquina del robot que lo cogió">
                          <Monitor size={12} aria-hidden="true" />
                          {paso.equipoNombre}
                        </span>
                      )}
                    </p>
                  )}

                  {paso.errorMensaje && (
                    <p className="mt-3 rounded-lg border border-red-200/70 bg-red-50/60 px-3 py-2 text-xs break-words whitespace-pre-wrap text-red-700">
                      {paso.errorMensaje}
                    </p>
                  )}
                </div>
              </TimelineItem>
            )
          })}
        </TimelineGrupo>
      </Timeline>

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
    </div>
  )
}
