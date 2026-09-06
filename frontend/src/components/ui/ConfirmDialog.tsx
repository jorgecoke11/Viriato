import { AnimatePresence, motion } from 'framer-motion'
import { useEffect, useState } from 'react'
import { overlayVariants, panelVariants } from '../../lib/motion/variants'
import { Button } from './Button'
import { Card } from './Card'

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
    <AnimatePresence>
      {open && (
        <motion.div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 px-4"
          variants={overlayVariants}
          initial="initial"
          animate="animate"
          exit="exit"
        >
          <motion.div variants={panelVariants} initial="initial" animate="animate" exit="exit">
            <Card className="w-full max-w-sm">
              <h2 className="mb-2 text-lg font-medium text-gray-900">{frozen.title}</h2>
              <p className="text-sm text-gray-600">{frozen.message}</p>
              <div className="mt-4 flex justify-end gap-2">
                <Button type="button" variant="ghost" onClick={onCancel}>
                  Cancelar
                </Button>
                <Button type="button" variant="danger" disabled={frozen.pending} onClick={frozen.onConfirm}>
                  {frozen.pending ? frozen.pendingLabel : frozen.confirmLabel}
                </Button>
              </div>
            </Card>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}
