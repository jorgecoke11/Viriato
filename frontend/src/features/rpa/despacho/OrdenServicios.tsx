import { ArrowDown, ArrowUp, GripVertical, Plus, X } from 'lucide-react'
import { useRef, useState } from 'react'

export interface ServicioEnOrden {
  id: string
  nombre: string
}

interface OrdenServiciosProps {
  /** The order, first to last. */
  orden: ServicioEnOrden[]
  /** Services that could be added to it: they run on the machine (or exist, for a template) but have no place yet. */
  sinPosicion: ServicioEnOrden[]
  onChange: (orden: ServicioEnOrden[]) => void
  disabled?: boolean
}

/**
 * The order a machine serves its services in. Reorder with the arrows (keyboard and touch friendly) or by dragging
 * a row; a service without a place yet waits under the list and goes after all of them until it is added.
 */
export function OrdenServicios({ orden, sinPosicion, onChange, disabled = false }: OrdenServiciosProps) {
  const arrastrando = useRef<number | null>(null)
  const [sobre, setSobre] = useState<number | null>(null)

  const mover = (desde: number, hasta: number) => {
    if (hasta < 0 || hasta >= orden.length || desde === hasta) return
    const copia = [...orden]
    const [elemento] = copia.splice(desde, 1)
    copia.splice(hasta, 0, elemento)
    onChange(copia)
  }

  const quitar = (indice: number) => onChange(orden.filter((_, i) => i !== indice))
  const anadir = (servicio: ServicioEnOrden) => onChange([...orden, servicio])

  return (
    <div className="flex flex-col gap-3">
      {orden.length === 0 ? (
        <p className="rounded-lg border border-dashed border-gray-300 px-4 py-5 text-center text-sm text-gray-500">
          Sin orden: los servicios se atienden por orden de llegada. Añade alguno para fijar cuál va primero.
        </p>
      ) : (
        <ol className="flex flex-col gap-1.5">
          {orden.map((servicio, indice) => (
            <li
              key={servicio.id}
              draggable={!disabled}
              onDragStart={(e) => {
                arrastrando.current = indice
                e.dataTransfer.effectAllowed = 'move'
              }}
              onDragOver={(e) => {
                e.preventDefault()
                setSobre(indice)
              }}
              onDragLeave={() => setSobre((actual) => (actual === indice ? null : actual))}
              onDrop={(e) => {
                e.preventDefault()
                if (arrastrando.current !== null) mover(arrastrando.current, indice)
                arrastrando.current = null
                setSobre(null)
              }}
              onDragEnd={() => {
                arrastrando.current = null
                setSobre(null)
              }}
              className={`flex items-center gap-2 rounded-lg border bg-surface px-2 py-1.5 ${
                sobre === indice ? 'border-indigo-400 ring-2 ring-indigo-500/20' : 'border-gray-200'
              }`}
            >
              <GripVertical size={16} className="shrink-0 cursor-grab text-gray-400" aria-hidden="true" />
              <span className="num flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-indigo-100 font-mono text-xs font-medium text-indigo-700">
                {indice + 1}
              </span>
              <span className="min-w-0 flex-1 truncate text-sm font-medium text-gray-900" title={servicio.nombre}>
                {servicio.nombre}
              </span>
              <span className="flex shrink-0 items-center">
                <button
                  type="button"
                  aria-label={`Subir ${servicio.nombre}`}
                  title="Subir"
                  disabled={disabled || indice === 0}
                  className="flex h-8 w-8 items-center justify-center rounded-md text-gray-500 hover:bg-gray-100 hover:text-gray-900 disabled:opacity-30"
                  onClick={() => mover(indice, indice - 1)}
                >
                  <ArrowUp size={16} />
                </button>
                <button
                  type="button"
                  aria-label={`Bajar ${servicio.nombre}`}
                  title="Bajar"
                  disabled={disabled || indice === orden.length - 1}
                  className="flex h-8 w-8 items-center justify-center rounded-md text-gray-500 hover:bg-gray-100 hover:text-gray-900 disabled:opacity-30"
                  onClick={() => mover(indice, indice + 1)}
                >
                  <ArrowDown size={16} />
                </button>
                <button
                  type="button"
                  aria-label={`Quitar ${servicio.nombre} del orden`}
                  title="Quitar del orden"
                  disabled={disabled}
                  className="flex h-8 w-8 items-center justify-center rounded-md text-gray-500 hover:bg-red-50 hover:text-red-600"
                  onClick={() => quitar(indice)}
                >
                  <X size={16} />
                </button>
              </span>
            </li>
          ))}
        </ol>
      )}

      {sinPosicion.length > 0 && (
        <div className="flex flex-col gap-1.5">
          <p className="text-xs font-medium tracking-wide text-gray-500 uppercase">Sin posición — van después</p>
          <ul className="flex flex-wrap gap-1.5">
            {sinPosicion.map((servicio) => (
              <li key={servicio.id}>
                <button
                  type="button"
                  disabled={disabled}
                  className="inline-flex items-center gap-1.5 rounded-full border border-dashed border-gray-300 px-3 py-1 text-sm text-gray-700 hover:border-indigo-400 hover:bg-indigo-50 hover:text-indigo-700"
                  onClick={() => anadir(servicio)}
                >
                  <Plus size={14} aria-hidden="true" />
                  {servicio.nombre}
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}
