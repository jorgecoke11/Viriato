import { Eye, EyeOff, Gauge, MonitorPlay, type LucideIcon } from 'lucide-react'
import type { BadgeTone } from '../../components/ui/Badge'
import type { OpcionEnTarjeta } from '../../components/ui/TarjetasDeOpciones'

export type CalidadVistaEnDirecto = 'Apagada' | 'Baja' | 'Media' | 'Alta'
export type GrabacionDeVideo = 'SegunRobot' | 'Activa' | 'Apagada'

/** The levels of the live view, as the platform defines them (the numbers live in the API: VistaEnDirectoPresets). */
export const calidadesDeVista: readonly (OpcionEnTarjeta & { id: CalidadVistaEnDirecto; icono: LucideIcon; tono: BadgeTone })[] = [
  { id: 'Apagada', titulo: 'Apagada', descripcion: 'Nadie ve la pantalla del robot y el robot no la sirve.', icono: EyeOff, tono: 'neutral' },
  { id: 'Baja', titulo: 'Baja', descripcion: 'Unas 2 imágenes por segundo, muy comprimidas. La que menos le cuesta al robot.', icono: Gauge, tono: 'info' },
  { id: 'Media', titulo: 'Media', descripcion: 'Unas 5 imágenes por segundo, calidad media.', icono: Eye, tono: 'brand' },
  { id: 'Alta', titulo: 'Alta', descripcion: 'Tan fluida como se pueda, como antes. La que más CPU le quita al robot.', icono: MonitorPlay, tono: 'warning' },
]

export const opcionesDeGrabacion: readonly { value: GrabacionDeVideo; label: string }[] = [
  { value: 'SegunRobot', label: 'Lo decide el robot' },
  { value: 'Activa', label: 'Grabar cada ejecución' },
  { value: 'Apagada', label: 'No grabar' },
]

export function calidadDeVista(id: string) {
  return calidadesDeVista.find((c) => c.id === id) ?? calidadesDeVista[0]
}

export function etiquetaDeGrabacion(valor: string) {
  return opcionesDeGrabacion.find((o) => o.value === valor)?.label ?? valor
}
