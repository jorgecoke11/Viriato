import { AnimatePresence, motion } from 'framer-motion'
import { useEffect, useState, type ReactNode } from 'react'
import { overlayVariants, panelVariants } from '../../lib/motion/variants'
import { Card } from './Card'

interface ModalProps {
  open: boolean
  title: ReactNode
  onClose: () => void
  children: ReactNode
  size?: 'md' | 'lg'
}

export function Modal({ open, title, onClose, children, size = 'md' }: ModalProps) {
  // The caller typically clears its underlying data the instant it sets `open` to false, but the
  // panel needs something valid to keep showing while its exit animation plays — so the last real
  // content is frozen here and only refreshed while the modal is actually open.
  const [frozen, setFrozen] = useState({ title, children, size })
  useEffect(() => {
    if (open) setFrozen({ title, children, size })
  }, [open, title, children, size])

  return (
    <AnimatePresence>
      {open && (
        <motion.div
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 px-4 py-8"
          variants={overlayVariants}
          initial="initial"
          animate="animate"
          exit="exit"
          onClick={onClose}
        >
          <motion.div
            className="w-full"
            style={{ display: 'flex', justifyContent: 'center' }}
            variants={panelVariants}
            initial="initial"
            animate="animate"
            exit="exit"
          >
            <Card
              className={`flex max-h-full w-full flex-col overflow-hidden p-0 ${frozen.size === 'lg' ? 'max-w-3xl' : 'max-w-lg'}`}
              onClick={(e) => e.stopPropagation()}
            >
              <div className="flex items-center justify-between border-b border-gray-200 px-5 py-3">
                <div className="text-sm font-medium text-gray-900">{frozen.title}</div>
                <button className="text-gray-400 hover:text-gray-600" onClick={onClose} aria-label="Cerrar">
                  ✕
                </button>
              </div>
              <div className="overflow-y-auto px-5 py-4">{frozen.children}</div>
            </Card>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}
