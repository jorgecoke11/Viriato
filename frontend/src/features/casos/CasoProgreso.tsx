import { Ban, Check, Circle, Clock, Eye, Loader2, SkipForward, X, type LucideIcon } from 'lucide-react'
import type { EstadoDelPaso, PasoDelProgreso } from './progreso'

const visual: Record<EstadoDelPaso, { icono: LucideIcon; circulo: string; texto: string; etiqueta: string; gira?: boolean }> = {
  completado: { icono: Check, circulo: 'bg-green-500 text-white', texto: 'text-green-700', etiqueta: 'Completado' },
  'en-curso': { icono: Loader2, circulo: 'bg-blue-500 text-white', texto: 'text-blue-700', etiqueta: 'En marcha', gira: true },
  'en-cola': { icono: Clock, circulo: 'bg-blue-100 text-blue-700 ring-2 ring-blue-300', texto: 'text-blue-700', etiqueta: 'En cola' },
  fallido: { icono: X, circulo: 'bg-red-500 text-white', texto: 'text-red-700', etiqueta: 'Fallido' },
  pendiente: { icono: Circle, circulo: 'bg-gray-100 text-gray-400 ring-1 ring-gray-300', texto: 'text-gray-500', etiqueta: 'Pendiente' },
  omitido: { icono: SkipForward, circulo: 'bg-gray-200 text-gray-500', texto: 'text-gray-500', etiqueta: 'Omitido' },
  cancelado: { icono: Ban, circulo: 'bg-gray-300 text-gray-600', texto: 'text-gray-500', etiqueta: 'Cancelado' },
  revision: { icono: Eye, circulo: 'bg-purple-500 text-white', texto: 'text-purple-700', etiqueta: 'Revisión' },
}

const resuelto = (estado: EstadoDelPaso) => estado === 'completado' || estado === 'omitido'

/**
 * Where the Caso is in its process: the steps in order, each with where it stands. The colour is never the only thing
 * that says it — every step also carries its icon and its state as a word. It scrolls sideways on a narrow screen
 * instead of squeezing the names.
 */
export function CasoProgreso({ pasos }: { pasos: PasoDelProgreso[] }) {
  if (pasos.length === 0) return <p className="text-sm text-gray-500">Este proceso no tiene pasos.</p>

  return (
    <ol className="flex overflow-x-auto pb-1" aria-label="Pasos del proceso">
      {pasos.map((paso, i) => {
        const { icono: Icono, circulo, texto, etiqueta, gira } = visual[paso.estado]
        const anteriorResuelto = i > 0 && resuelto(pasos[i - 1].estado)
        return (
          <li key={paso.id} className="flex min-w-[9rem] flex-1 flex-col items-center text-center">
            <div className="flex w-full items-center">
              <span aria-hidden="true" className={`h-0.5 flex-1 ${i === 0 ? 'bg-transparent' : anteriorResuelto ? 'bg-green-500' : 'bg-gray-200'}`} />
              <span className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-full ${circulo}`}>
                <Icono size={16} aria-hidden="true" className={gira ? 'motion-safe:animate-spin' : ''} />
              </span>
              <span
                aria-hidden="true"
                className={`h-0.5 flex-1 ${i === pasos.length - 1 ? 'bg-transparent' : resuelto(paso.estado) ? 'bg-green-500' : 'bg-gray-200'}`}
              />
            </div>
            <p className="mt-2 max-w-[10rem] px-1 text-sm leading-tight font-medium text-gray-900">{paso.nombre}</p>
            <p className={`mt-0.5 text-xs font-medium ${texto}`}>
              {etiqueta}
              {paso.intentos > 1 && <span className="font-normal text-amber-700"> · intento {paso.intentos}</span>}
            </p>
          </li>
        )
      })}
    </ol>
  )
}
