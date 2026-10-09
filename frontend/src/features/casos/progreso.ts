import type { EjecucionPasoDto, FlujoPasoDefDto } from './api'

/** Where one step of the process stands for a Caso, in the terms people use. */
export type EstadoDelPaso = 'completado' | 'en-curso' | 'en-cola' | 'fallido' | 'pendiente' | 'omitido' | 'cancelado' | 'revision'

export interface PasoDelProgreso {
  id: string
  nombre: string
  tipoPaso: string
  estado: EstadoDelPaso
  /** How many attempts it has had (0 if it never ran). */
  intentos: number
  /** The latest attempt, the one that says where the step stands. Null if it never ran. */
  ultimo: EjecucionPasoDto | null
}

function estadoDe(paso: EjecucionPasoDto | null): EstadoDelPaso {
  if (paso === null) return 'pendiente'
  switch (paso.estado) {
    case 'Completado':
      return 'completado'
    case 'Fallido':
      return 'fallido'
    case 'Cancelado':
      return 'cancelado'
    case 'Omitido':
      return 'omitido'
    case 'EsperandoRevisionHumana':
      return 'revision'
    case 'EnProgreso':
      return paso.enCola ? 'en-cola' : 'en-curso'
    default:
      return 'pendiente'
  }
}

/**
 * The steps of the process, in order, each with where it stands for this Caso: what its latest attempt says, or
 * "pendiente" if it has not run. Earlier attempts do not count — a step that failed and was reprocessed is where its
 * newest attempt is.
 */
export function progresoDelProceso(definiciones: readonly FlujoPasoDefDto[], pasos: readonly EjecucionPasoDto[]): PasoDelProgreso[] {
  return [...definiciones]
    .sort((a, b) => a.orden - b.orden)
    .map((definicion) => {
      const intentos = pasos.filter((p) => p.flujoPasoDefId === definicion.id)
      const ultimo = intentos.reduce<EjecucionPasoDto | null>((mejor, p) => (mejor === null || p.numeroIntento > mejor.numeroIntento ? p : mejor), null)
      return { id: definicion.id, nombre: definicion.nombre, tipoPaso: definicion.tipoPaso, estado: estadoDe(ultimo), intentos: intentos.length, ultimo }
    })
}

/** How many steps are settled (done or deliberately skipped) out of the ones the process has. */
export function pasosResueltos(progreso: readonly PasoDelProgreso[]): number {
  return progreso.filter((p) => p.estado === 'completado' || p.estado === 'omitido').length
}
