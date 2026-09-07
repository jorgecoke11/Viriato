import { useEffect, useState } from 'react'
import { Button } from './Button'
import { Modal } from './Modal'

interface ConfirmDialogProps {
  open: boolean
  title: string
  message: string
  confirmLabel?: string
  pendingLabel?: string
  onConfirm: () => void
  onCancel: () => void
  pending?: boolean
}

export function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel = 'Eliminar',
  pendingLabel = 'Eliminando…',
  onConfirm,
  onCancel,
  pending,
}: ConfirmDialogProps) {
  // Frozen so the dialog keeps showing valid content while it animates out, even if the caller
  // clears the underlying data (e.g. the item pending deletion) the moment `open` goes false.
  const [frozen, setFrozen] = useState({ title, message, confirmLabel, pendingLabel, onConfirm, pending })
  useEffect(() => {
    if (open) setFrozen({ title, message, confirmLabel, pendingLabel, onConfirm, pending })
  }, [open, title, message, confirmLabel, pendingLabel, onConfirm, pending])

  return (
    <Modal
      open={open}
      title={frozen.title}
      onClose={onCancel}
      size="sm"
      footer={
        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onCancel}>
            Cancelar
          </Button>
          <Button type="button" variant="danger" disabled={frozen.pending} onClick={frozen.onConfirm}>
            {frozen.pending ? frozen.pendingLabel : frozen.confirmLabel}
          </Button>
        </div>
      }
    >
      <p className="text-sm text-gray-600">{frozen.message}</p>
    </Modal>
  )
}
