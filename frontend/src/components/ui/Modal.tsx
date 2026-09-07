import { AnimatePresence, motion } from 'framer-motion'
import { useEffect, useState, type ReactNode } from 'react'
import { overlayVariants, panelVariants } from '../../lib/motion/variants'
import { Card } from './Card'

interface ModalProps {
  open: boolean
  title: ReactNode
  onClose: () => void
  children: ReactNode
  /** Pinned below the scrollable body — e.g. Cancelar/Guardar — so actions stay reachable even when
   * the body itself scrolls. Omit for modals with no actions of their own (media viewers, etc.). */
  footer?: ReactNode
  size?: 'sm' | 'md' | 'lg'
}

const sizeClasses = { sm: 'max-w-sm', md: 'max-w-lg', lg: 'max-w-3xl' }

export function Modal({ open, title, onClose, children, footer, size = 'md' }: ModalProps) {
  // The caller typically clears its underlying data the instant it sets `open` to false, but the
  // panel needs something valid to keep showing while its exit animation plays — so the last real
  // content is frozen here and only refreshed while the modal is actually open.
  const [frozen, setFrozen] = useState({ title, children, footer, size })
  useEffect(() => {
    if (open) setFrozen({ title, children, footer, size })
  }, [open, title, children, footer, size])

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
            className="flex w-full justify-center"
            variants={panelVariants}
            initial="initial"
            animate="animate"
            exit="exit"
          >
            {/* max-h is viewport-relative rather than max-h-full: a flex-centered ancestor never
                resolves a percentage height (its own height depends on this element's content), so
                max-h-full would silently do nothing and let a tall body push past the viewport. */}
            <Card
              className={`flex max-h-[85vh] w-full flex-col overflow-hidden p-0 ${sizeClasses[frozen.size]}`}
              onClick={(e) => e.stopPropagation()}
            >
              <div className="flex shrink-0 items-center justify-between border-b border-gray-200 px-5 py-3">
                <div className="text-sm font-medium text-gray-900">{frozen.title}</div>
                <button className="text-gray-400 hover:text-gray-600" onClick={onClose} aria-label="Cerrar">
                  ✕
                </button>
              </div>
              <div className="overflow-y-auto px-5 py-4">{frozen.children}</div>
              {frozen.footer && <div className="shrink-0 border-t border-gray-200 px-5 py-3">{frozen.footer}</div>}
            </Card>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}
