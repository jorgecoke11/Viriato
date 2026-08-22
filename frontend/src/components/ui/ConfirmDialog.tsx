import { Button } from './Button'
import { Card } from './Card'

export function ConfirmDialog({
  title,
  message,
  confirmLabel = 'Eliminar',
  onConfirm,
  onCancel,
  pending,
}: {
  title: string
  message: string
  confirmLabel?: string
  onConfirm: () => void
  onCancel: () => void
  pending?: boolean
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 px-4">
      <Card className="w-full max-w-sm">
        <h2 className="mb-2 text-lg font-medium text-gray-900">{title}</h2>
        <p className="text-sm text-gray-600">{message}</p>
        <div className="mt-4 flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onCancel}>
            Cancelar
          </Button>
          <Button type="button" disabled={pending} onClick={onConfirm}>
            {pending ? 'Eliminando…' : confirmLabel}
          </Button>
        </div>
      </Card>
    </div>
  )
}
