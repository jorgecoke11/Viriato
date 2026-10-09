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
  /** For what is big or cannot be undone: the confirm button stays off until this exact word is typed. */
  requireText?: string
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
  requireText,
}: ConfirmDialogProps) {
  const [typed, setTyped] = useState('')

  // Every time it opens it starts empty: a confirmation typed earlier must not carry over to the next one.
  useEffect(() => {
    if (open) setTyped('')
  }, [open])

  const confirmed = requireText === undefined || typed.trim().toLowerCase() === requireText.toLowerCase()

  // Frozen so the dialog keeps showing valid content while it animates out, even if the caller
  // clears the underlying data (e.g. the item pending deletion) the moment `open` goes false.
  const [frozen, setFrozen] = useState({ title, message, confirmLabel, pendingLabel, onConfirm, pending, requireText })
  useEffect(() => {
    if (open) setFrozen({ title, message, confirmLabel, pendingLabel, onConfirm, pending, requireText })
  }, [open, title, message, confirmLabel, pendingLabel, onConfirm, pending, requireText])

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
          <Button type="button" variant="danger" disabled={frozen.pending || !confirmed} onClick={frozen.onConfirm}>
            {frozen.pending ? frozen.pendingLabel : frozen.confirmLabel}
          </Button>
        </div>
      }
    >
      <p className="text-sm text-gray-600">{frozen.message}</p>
      {frozen.requireText !== undefined && (
        <form
          className="mt-4 flex flex-col gap-1.5"
          onSubmit={(e) => {
            e.preventDefault()
            if (confirmed && !frozen.pending) frozen.onConfirm()
          }}
        >
          <label htmlFor="confirmar-texto" className="text-sm font-medium text-gray-700">
            Para confirmar, escribe <span className="font-mono font-semibold text-gray-900">{frozen.requireText}</span>
          </label>
          <input
            id="confirmar-texto"
            className="field"
            autoComplete="off"
            value={typed}
            onChange={(e) => setTyped(e.target.value)}
          />
        </form>
      )}
    </Modal>
  )
}
