import { useQuery } from '@tanstack/react-query'
import { Activity, Bot, Hourglass } from 'lucide-react'
import { Badge } from '../../../components/ui/Badge'
import { Skeleton } from '../../../components/ui/Skeleton'
import { useAuth } from '../../auth/useAuth'
import { CancelarEjecucion } from '../../casos/CancelarEjecucion'
import { PrioridadChip } from '../../casos/PrioridadChip'
import * as rpaApi from '../api'
import type { EnEjecucionDto, PendienteDto } from '../api'

const MAX_VISIBLES = 12

function hace(fecha: string | null): string {
  if (!fecha) return ''
  const segundos = Math.max(0, Math.round((Date.now() - new Date(fecha).getTime()) / 1000))
  if (segundos < 60) return 'hace unos segundos'
  const minutos = Math.round(segundos / 60)
  if (minutos < 60) return `hace ${minutos} min`
  const horas = Math.round(minutos / 60)
  return horas < 48 ? `hace ${horas} h` : `hace ${Math.round(horas / 24)} d`
}

function duracion(minutos: number): string {
  if (minutos < 1) return 'menos de 1 min'
  if (minutos < 60) return `${Math.round(minutos)} min`
  const horas = Math.floor(minutos / 60)
  const resto = Math.round(minutos % 60)
  return resto === 0 ? `${horas} h` : `${horas} h ${resto} min`
}

// How long a running step has left before its service's maximum time cancels the caso — or that it has none, which
// means a robot that dies would hold the machine's slot for ever.
function AvisoTiempo({ ejecucion }: { ejecucion: EnEjecucionDto }) {
  if (ejecucion.vencido) return <Badge tone="danger">Pasado de tiempo</Badge>
  if (ejecucion.limiteAt === null) return <Badge tone="warning">Sin tiempo máximo</Badge>
  const quedan = (new Date(ejecucion.limiteAt).getTime() - Date.now()) / 60000
  return <Badge tone={quedan < 5 ? 'warning' : 'neutral'}>Quedan {duracion(Math.max(0, quedan))}</Badge>
}

// Why a step is not moving: the robot that would take it is off, not answering, or busy with something else.
function AvisoRobot({ pendiente }: { pendiente: PendienteDto }) {
  if (!pendiente.robotEncendido) return <Badge tone="neutral">Robot apagado</Badge>
  if (!pendiente.robotConectado) return <Badge tone="warning">Robot sin conexión</Badge>
  if (pendiente.robotOcupado) return <Badge tone="info">Robot ocupado</Badge>
  if (pendiente.servicioAlLimiteGlobal) return <Badge tone="info">Servicio al límite</Badge>
  return null
}

/**
 * What a machine is doing right now and what is waiting for it, in the order it will be served. It asks the same
 * code that hands the steps out, so what it shows is what will happen. It refreshes by itself; it shows the saved
 * configuration, not what is being edited next to it.
 */
export function ColaEquipoPanel({ equipoId, activo }: { equipoId: string; activo: boolean }) {
  // Changing where an execution stands in its queue is an action on Casos, so it needs their permission, not the fleet's.
  const puedeCambiarPrioridad = useAuth().can('casos.prioridad')
  const cola = useQuery({
    queryKey: ['cola-equipo', equipoId],
    queryFn: () => rpaApi.getColaEquipo(equipoId),
    enabled: activo,
    refetchInterval: 5000,
  })

  if (cola.isLoading) {
    return (
      <div role="status" aria-label="Cargando la cola" className="flex flex-col gap-2">
        <Skeleton className="h-6 w-1/2" />
        <Skeleton className="h-12" />
        <Skeleton className="h-12" />
      </div>
    )
  }

  if (cola.isError || !cola.data) {
    return <p className="text-sm text-red-600">No se ha podido cargar la cola de la máquina.</p>
  }

  const { enUso, maxEjecucionesSimultaneas: max, enEjecucion, pendientes, robots } = cola.data
  const libresEnTotal = robots.filter((r) => r.encendido).reduce((suma, r) => suma + r.libres, 0)
  const visibles = pendientes.slice(0, MAX_VISIBLES)

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col gap-1.5">
        <div className="flex items-baseline justify-between">
          <span className="text-sm font-medium text-gray-700">Ahora mismo</span>
          <span className="num font-mono text-sm text-gray-900">
            {max === null ? enUso : `${enUso} de ${max}`} <span className="font-sans text-gray-500">en uso</span>
          </span>
        </div>
        {max !== null ? (
          <div role="img" aria-label={`${enUso} de ${max} ejecuciones en uso`} className="flex h-2 gap-0.5 overflow-hidden rounded-full">
            {Array.from({ length: Math.min(max, 12) }, (_, i) => (
              <span key={i} className={`flex-1 ${i < enUso ? 'bg-blue-500' : 'bg-gray-200'}`} />
            ))}
          </div>
        ) : (
          <p className="text-xs text-gray-500">
            Sin límite: la capacidad son las copias de los robots.{' '}
            <span className="num font-mono text-gray-700">{libresEnTotal}</span> {libresEnTotal === 1 ? 'libre' : 'libres'} ahora.
          </p>
        )}
      </div>

      <section className="flex flex-col gap-1.5">
        <h3 className="flex items-center gap-1.5 text-xs font-medium tracking-wide text-gray-500 uppercase">
          <Bot size={13} aria-hidden="true" /> Robots y sus copias
        </h3>
        {robots.length === 0 ? (
          <p className="text-sm text-gray-500">Esta máquina no tiene robots desplegados.</p>
        ) : (
          <ul className="flex flex-col gap-1.5">
            {robots.map((r) => (
              <li key={r.despliegueId} className="flex items-center gap-2 rounded-lg border border-gray-200 bg-surface px-3 py-2">
                <span className="min-w-0 flex-1 truncate text-sm font-medium text-gray-900">{r.servicioNombre}</span>
                {!r.encendido ? (
                  <Badge tone="neutral">Apagado</Badge>
                ) : r.instancias === 0 ? (
                  <Badge tone="warning">Sin copias en marcha</Badge>
                ) : (
                  <span className="num shrink-0 text-xs text-gray-600">
                    {r.instancias} {r.instancias === 1 ? 'copia' : 'copias'} · {r.libres} {r.libres === 1 ? 'libre' : 'libres'}
                  </span>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="flex flex-col gap-1.5">
        <h3 className="flex items-center gap-1.5 text-xs font-medium tracking-wide text-gray-500 uppercase">
          <Activity size={13} aria-hidden="true" /> En ejecución
        </h3>
        {enEjecucion.length === 0 ? (
          <p className="text-sm text-gray-500">Nada en ejecución.</p>
        ) : (
          <ul className="flex flex-col gap-1.5">
            {enEjecucion.map((e) => (
              <li key={`${e.casoId}-${e.servicioId}`} className="flex items-center gap-2 rounded-lg border border-blue-200 bg-blue-50 px-3 py-2">
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-medium text-gray-900">{e.casoTitulo}</span>
                  <span className="block truncate text-xs text-gray-600">{e.servicioNombre}</span>
                </span>
                <span className="flex shrink-0 flex-col items-end gap-1">
                  <span className="text-xs text-gray-500">{hace(e.desde)}</span>
                  <AvisoTiempo ejecucion={e} />
                </span>
                {puedeCambiarPrioridad && <CancelarEjecucion variante="icono" casoId={e.casoId} ejecucionPasoId={e.ejecucionPasoId} nombre={e.casoTitulo} />}
              </li>
            ))}
          </ul>
        )}
      </section>

      <section className="flex flex-col gap-1.5">
        <h3 className="flex items-center gap-1.5 text-xs font-medium tracking-wide text-gray-500 uppercase">
          <Hourglass size={13} aria-hidden="true" /> Esperando, en este orden
        </h3>
        {pendientes.length === 0 ? (
          <p className="text-sm text-gray-500">No hay nada esperando.</p>
        ) : (
          <ol className="flex flex-col gap-1.5">
            {visibles.map((p) => (
              <li key={`${p.casoId}-${p.servicioId}-${p.posicion}`} className="flex items-center gap-2 rounded-lg border border-gray-200 bg-surface px-3 py-2">
                <span className="num flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-gray-100 font-mono text-xs text-gray-700">
                  {p.posicion}
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate text-sm font-medium text-gray-900">{p.casoTitulo}</span>
                  <span className="block truncate text-xs text-gray-600">
                    {p.servicioNombre} · {hace(p.esperaDesde)}
                  </span>
                  <span className="mt-1.5 flex">
                    <PrioridadChip casoId={p.casoId} ejecucionPasoId={p.ejecucionPasoId} prioridad={p.prioridad} puedeEditar={puedeCambiarPrioridad} />
                  </span>
                </span>
                {puedeCambiarPrioridad && <CancelarEjecucion variante="icono" casoId={p.casoId} ejecucionPasoId={p.ejecucionPasoId} nombre={p.casoTitulo} />}
                <span className="flex shrink-0 flex-col items-end gap-1">
                  {p.posicion === 1 && (max === null || enUso < max) && <Badge tone="brand">Siguiente</Badge>}
                  <AvisoRobot pendiente={p} />
                </span>
              </li>
            ))}
          </ol>
        )}
        {pendientes.length > MAX_VISIBLES && <p className="text-xs text-gray-500">…y {pendientes.length - MAX_VISIBLES} más.</p>}
      </section>
    </div>
  )
}
