import type { BadgeTone } from '../../../components/ui/Badge'
import type { LanzadorDelRobotDto } from '../api'

/** How a robot that is started on demand is shown: a short label, its tone, and the launcher's own explanation. */
export interface LanzadorEnPantalla {
  tono: BadgeTone
  texto: string
  /** Why, in the launcher's words: what it waits for or what failed. Null when there is nothing to add. */
  detalle: string | null
  /** Something is running right now. */
  enMarcha: boolean
}

function copias(n: number): string {
  return `${n} ${n === 1 ? 'copia' : 'copias'}`
}

/**
 * What to say about a robot whose launcher reported. A robot nobody launches (null) is not this function's business:
 * it is simply expected to be running, and the panel says so its own way.
 */
export function lanzadorEnPantalla(lanzador: LanzadorDelRobotDto): LanzadorEnPantalla {
  const detalle = lanzador.detalle?.trim() || null

  switch (lanzador.estado) {
    case 'EnMarcha':
      return {
        tono: 'success',
        texto: lanzador.replicas > 1 ? `${lanzador.copias} de ${copias(lanzador.replicas)} en marcha` : 'En marcha',
        detalle,
        enMarcha: true,
      }
    case 'EnEspera':
      return { tono: 'warning', texto: 'Esperando para arrancar', detalle, enMarcha: false }
    case 'Fallo':
      return { tono: 'danger', texto: 'No arranca', detalle, enMarcha: false }
    case 'Apagado':
      return { tono: 'neutral', texto: 'Apagado', detalle, enMarcha: false }
    default:
      // SinTrabajo — and anything a newer launcher may say that this screen does not know yet.
      return { tono: 'neutral', texto: 'Sin trabajo: arrancará solo', detalle, enMarcha: false }
  }
}

/**
 * Why a waiting step is not moving when its robot is not connected but a launcher looks after it: the robot is about to be
 * started, is waiting for room, or cannot be started. Without a launcher (null) the caller says «sin conexión» as always.
 */
export function avisoDePendiente(lanzador: LanzadorDelRobotDto | null | undefined): { tono: BadgeTone; texto: string; detalle: string | null } | null {
  if (!lanzador) return null
  const detalle = lanzador.detalle?.trim() || null

  if (lanzador.estado === 'Fallo') return { tono: 'danger', texto: 'El robot no arranca', detalle }
  if (lanzador.estado === 'EnEspera') return { tono: 'warning', texto: 'Esperando para arrancar', detalle }
  return { tono: 'info', texto: 'Arrancando el robot', detalle }
}
