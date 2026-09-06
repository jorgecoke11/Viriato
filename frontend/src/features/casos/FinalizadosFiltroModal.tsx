import { useEffect, useState } from 'react'
import { Button } from '../../components/ui/Button'
import { Input } from '../../components/ui/Input'
import { Modal } from '../../components/ui/Modal'
import type { FinalizadosFiltro } from './finalizadosFiltro'

const OPTIONS: { value: FinalizadosFiltro['tipo']; label: string }[] = [
  { value: 'hoy', label: 'Solo los finalizados de hoy' },
  { value: 'todos', label: 'Todos los finalizados' },
  { value: 'rango', label: 'Rango de fechas personalizado' },
]

/**
 * Shared by the general (dashboard-wide) and per-process filter buttons — the only difference is
 * whether `onUseGlobal` is passed (per-process only, to drop the override and fall back to the
 * dashboard's own setting).
 */
export function FinalizadosFiltroModal({
  open,
  title,
  value,
  onApply,
  onClose,
  onUseGlobal,
}: {
  open: boolean
  title: string
  value: FinalizadosFiltro
  onApply: (filtro: FinalizadosFiltro) => void
  onClose: () => void
  onUseGlobal?: () => void
}) {
  const [tipo, setTipo] = useState<FinalizadosFiltro['tipo']>(value.tipo)
  const [desde, setDesde] = useState(value.tipo === 'rango' ? (value.desde ?? '') : '')
  const [hasta, setHasta] = useState(value.tipo === 'rango' ? (value.hasta ?? '') : '')

  // Re-seeds every time the modal opens, matching whatever is currently applied — a fresh session
  // each time rather than remembering an abandoned edit from the last time it was open.
  useEffect(() => {
    if (open) {
      setTipo(value.tipo)
      setDesde(value.tipo === 'rango' ? (value.desde ?? '') : '')
      setHasta(value.tipo === 'rango' ? (value.hasta ?? '') : '')
    }
  }, [open, value])

  function handleApply() {
    onApply(tipo === 'rango' ? { tipo: 'rango', desde: desde || undefined, hasta: hasta || undefined } : { tipo })
    onClose()
  }

  return (
    <Modal open={open} title={title} onClose={onClose}>
      <div className="flex flex-col gap-3">
        <p className="text-sm text-gray-500">Los casos en curso siempre se muestran — esto solo decide qué finalizados aparecen.</p>

        <div className="flex flex-col gap-2">
          {OPTIONS.map((opt) => (
            <label key={opt.value} className="flex items-center gap-2 text-sm text-gray-700">
              <input
                type="radio"
                name="finalizados-filtro"
                className="accent-indigo-600"
                checked={tipo === opt.value}
                onChange={() => setTipo(opt.value)}
              />
              {opt.label}
            </label>
          ))}
        </div>

        {tipo === 'rango' && (
          <div className="grid grid-cols-2 gap-3 pl-6">
            <Input label="Desde" name="desde" type="date" value={desde} onChange={(e) => setDesde(e.target.value)} />
            <Input label="Hasta" name="hasta" type="date" value={hasta} onChange={(e) => setHasta(e.target.value)} />
          </div>
        )}

        <div className="mt-2 flex items-center justify-between border-t border-gray-100 pt-4">
          {onUseGlobal ? (
            <button
              type="button"
              className="text-sm font-medium text-gray-500 hover:text-gray-700"
              onClick={() => {
                onUseGlobal()
                onClose()
              }}
            >
              Usar el ajuste general
            </button>
          ) : (
            <span />
          )}
          <div className="flex gap-2">
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancelar
            </Button>
            <Button type="button" onClick={handleApply}>
              Aplicar
            </Button>
          </div>
        </div>
      </div>
    </Modal>
  )
}
