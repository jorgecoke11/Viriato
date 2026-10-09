import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ArrowDown, ArrowUp, ChevronDown, Minus, Plus } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Button } from '../../components/ui/Button'
import { Popover } from '../../components/ui/Popover'
import { ApiError } from '../../lib/apiClient'
import { useToast } from '../../lib/toast/useToast'
import * as casosApi from './api'
import { describirPrioridad, NIVELES_DE_PRIORIDAD, PRIORIDAD_MAXIMA, PRIORIDAD_MINIMA, validarPrioridad, type TonoDePrioridad } from './prioridad'

const tonos: Record<TonoDePrioridad, { caja: string; icono: typeof ArrowUp }> = {
  alta: { caja: 'bg-indigo-100 text-indigo-700 hover:bg-indigo-200', icono: ArrowUp },
  normal: { caja: 'bg-gray-100 text-gray-600 hover:bg-gray-200', icono: Minus },
  baja: { caja: 'bg-gray-100 text-gray-500 hover:bg-gray-200', icono: ArrowDown },
}

function Pastilla({ prioridad }: { prioridad: number }) {
  const { texto, tono } = describirPrioridad(prioridad)
  const Icono = tonos[tono].icono
  return (
    <>
      <Icono size={12} aria-hidden="true" />
      {texto}
    </>
  )
}

/**
 * The priority of an execution that waits for a robot, as a small pill: its level at a glance ("Urgente", "Normal"…).
 * For whoever may change it, the pill opens a panel with the usual levels and a field for any other value; the
 * change takes effect at once on the queue. For everybody else it is only the pill.
 */
export function PrioridadChip({
  casoId,
  ejecucionPasoId,
  prioridad,
  puedeEditar,
}: {
  casoId: string
  ejecucionPasoId: string
  prioridad: number
  puedeEditar: boolean
}) {
  const { tono } = describirPrioridad(prioridad)
  const base = 'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium'

  if (!puedeEditar) {
    return (
      <span className={`${base} ${tonos[tono].caja.split(' ').slice(0, 2).join(' ')}`} title="Prioridad en la cola">
        <Pastilla prioridad={prioridad} />
      </span>
    )
  }

  return (
    <Popover
      label="Prioridad en la cola"
      trigger={({ open, onClick, ref, ...aria }) => (
        <button
          type="button"
          ref={ref}
          onClick={(e) => {
            e.stopPropagation()
            onClick()
          }}
          title="Cambiar la prioridad en la cola"
          className={`${base} ${tonos[tono].caja} focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-500 ${open ? 'ring-2 ring-indigo-400' : ''}`}
          {...aria}
        >
          <Pastilla prioridad={prioridad} />
          <ChevronDown size={12} aria-hidden="true" className={`transition-transform ${open ? 'rotate-180' : ''}`} />
        </button>
      )}
    >
      {(close) => <PanelDePrioridad casoId={casoId} ejecucionPasoId={ejecucionPasoId} prioridad={prioridad} close={close} />}
    </Popover>
  )
}

function PanelDePrioridad({
  casoId,
  ejecucionPasoId,
  prioridad,
  close,
}: {
  casoId: string
  ejecucionPasoId: string
  prioridad: number
  close: () => void
}) {
  const queryClient = useQueryClient()
  const { showToast } = useToast()
  const [texto, setTexto] = useState(String(prioridad))
  const campo = useRef<HTMLInputElement>(null)

  useEffect(() => {
    campo.current?.focus()
    campo.current?.select()
  }, [])

  const error = validarPrioridad(texto)
  const valor = Number(texto)
  const cambiada = error === null && valor !== prioridad

  const guardar = useMutation({
    mutationFn: () => casosApi.cambiarPrioridad(casoId, ejecucionPasoId, valor),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['caso', casoId] })
      queryClient.invalidateQueries({ queryKey: ['caso-ejecucion', casoId] })
      queryClient.invalidateQueries({ queryKey: ['cola-equipo'] })
      showToast('success', 'Prioridad actualizada.')
      close()
    },
    onError: (err) => showToast('error', err instanceof ApiError ? err.message : 'No se pudo cambiar la prioridad.'),
  })

  const mover = (delta: number) => {
    const actual = error === null ? valor : prioridad
    setTexto(String(Math.min(PRIORIDAD_MAXIMA, Math.max(PRIORIDAD_MINIMA, actual + delta))))
  }

  return (
    <form
      className="flex flex-col gap-3"
      onSubmit={(e) => {
        e.preventDefault()
        if (cambiada && !guardar.isPending) guardar.mutate()
      }}
    >
      <div>
        <h4 className="text-sm font-semibold text-gray-900">Prioridad en la cola</h4>
        <p className="mt-0.5 text-xs text-gray-500">Mayor va antes entre lo que espera al mismo servicio.</p>
      </div>

      <div role="group" aria-label="Niveles habituales" className="grid grid-cols-4 gap-1.5">
        {NIVELES_DE_PRIORIDAD.map((nivel) => {
          const activo = error === null && valor === nivel.valor
          return (
            <button
              key={nivel.valor}
              type="button"
              aria-pressed={activo}
              onClick={() => setTexto(String(nivel.valor))}
              className={`flex flex-col items-center rounded-lg border px-1 py-1.5 text-xs font-medium transition-colors focus-visible:outline-2 focus-visible:outline-indigo-500 ${
                activo
                  ? 'border-indigo-500 bg-indigo-50 text-indigo-700'
                  : 'border-gray-200 bg-surface text-gray-700 hover:border-gray-300 hover:bg-gray-50'
              }`}
            >
              {nivel.nombre}
              <span className="num font-mono text-[11px] font-normal opacity-70">{nivel.valor > 0 ? `+${nivel.valor}` : nivel.valor}</span>
            </button>
          )
        })}
      </div>

      <div className="flex flex-col gap-1">
        <label htmlFor={`prioridad-${ejecucionPasoId}`} className="text-xs font-medium text-gray-600">
          Otro valor
        </label>
        <div className="flex items-center gap-1.5">
          <button
            type="button"
            aria-label="Bajar prioridad"
            onClick={() => mover(-1)}
            className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border border-gray-300 bg-surface text-gray-600 hover:bg-gray-50 focus-visible:outline-2 focus-visible:outline-indigo-500"
          >
            <Minus size={14} aria-hidden="true" />
          </button>
          <input
            id={`prioridad-${ejecucionPasoId}`}
            ref={campo}
            type="number"
            inputMode="numeric"
            aria-invalid={error ? true : undefined}
            aria-describedby={error ? `prioridad-error-${ejecucionPasoId}` : undefined}
            className={`field num h-8 min-w-0 flex-1 py-0 text-center ${error ? 'border-red-500' : ''}`}
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
          />
          <button
            type="button"
            aria-label="Subir prioridad"
            onClick={() => mover(1)}
            className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border border-gray-300 bg-surface text-gray-600 hover:bg-gray-50 focus-visible:outline-2 focus-visible:outline-indigo-500"
          >
            <Plus size={14} aria-hidden="true" />
          </button>
        </div>
        {error && (
          <p id={`prioridad-error-${ejecucionPasoId}`} className="text-xs text-red-600">
            {error}
          </p>
        )}
      </div>

      <div className="flex justify-end gap-2">
        <Button type="button" variant="ghost" size="sm" onClick={close}>
          Cancelar
        </Button>
        <Button type="submit" size="sm" disabled={!cambiada || guardar.isPending}>
          {guardar.isPending ? 'Guardando…' : 'Guardar'}
        </Button>
      </div>
    </form>
  )
}
