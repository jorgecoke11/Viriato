import { Plus, SlidersHorizontal, type LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import type { FlujoResumenDto } from './api'
import { NuevoCasoModal } from './NuevoCasoModal'
import { ParametrosDelProcesoModal } from './ParametrosDelProcesoModal'

/** What an action's window receives: whether it is open, the process whose card it was opened from, and how to close it. */
export interface PropsDeAccionDeProceso {
  abierto: boolean
  resumen: FlujoResumenDto
  alCerrar: () => void
}

/**
 * Something a person can do to a process from its card on the dashboard: an icon button and the window it opens. The card draws
 * whichever of these apply, so a new action is one more entry in {@link ACCIONES_DE_PROCESO} and nothing in the card changes.
 */
export interface AccionDeProceso {
  id: string
  etiqueta: (resumen: FlujoResumenDto) => string
  icono: LucideIcon
  /** The permission it takes. */
  permiso: string
  /** Whether it makes sense for this process: one with nothing to change does not offer "change parameters". Default: always. */
  disponible?: (resumen: FlujoResumenDto) => boolean
  /** The main thing to do on the card (drawn filled). */
  principal?: boolean
  /** Draws the window the button opens. */
  ventana: (props: PropsDeAccionDeProceso) => ReactNode
}

/** In the order they are drawn, left to right. */
export const ACCIONES_DE_PROCESO: readonly AccionDeProceso[] = [
  {
    id: 'parametros',
    etiqueta: (r) => `Cambiar parámetros de ${r.flujoNombre}`,
    icono: SlidersHorizontal,
    permiso: 'flujos.parametros',
    disponible: (r) => r.parametrosEditables > 0,
    ventana: ({ abierto, resumen, alCerrar }) => (
      <ParametrosDelProcesoModal abierto={abierto} flujoId={resumen.flujoId} flujoNombre={resumen.flujoNombre} alCerrar={alCerrar} />
    ),
  },
  {
    id: 'nuevo-caso',
    etiqueta: (r) => `Añadir caso a ${r.flujoNombre}`,
    icono: Plus,
    permiso: 'casos.crear',
    principal: true,
    ventana: ({ abierto, resumen, alCerrar }) => (
      <NuevoCasoModal open={abierto} flujoId={resumen.flujoId} flujoNombre={resumen.flujoNombre} onClose={alCerrar} />
    ),
  },
]
