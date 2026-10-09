import { AnimatePresence, motion } from 'framer-motion'
import { X } from 'lucide-react'
import { useEffect, useId, useState, type ReactNode } from 'react'
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
  size?: 'sm' | 'md' | 'lg' | 'xl'
}

const sizeClasses = { sm: 'max-w-sm', md: 'max-w-lg', lg: 'max-w-3xl', xl: 'max-w-5xl' }

export function Modal({ open, title, onClose, children, footer, size = 'md' }: ModalProps) {
  const titleId = useId()

  // The caller typically clears its underlying data the instant it sets `open` to false, but the
  // panel needs something valid to keep showing while its exit animation plays — so the last real
  // content is frozen here and only refreshed while the modal is actually open.
  const [frozen, setFrozen] = useState({ title, children, footer, size })
  useEffect(() => {
    if (open) setFrozen({ title, children, footer, size })
  }, [open, title, children, footer, size])

  // Escape closes it, as people expect of any dialog.
  useEffect(() => {
    if (!open) return
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') onClose()
    }
    document.addEventListener('keydown', onKeyDown)
    return () => document.removeEventListener('keydown', onKeyDown)
  }, [open, onClose])

  return (
    <AnimatePresence>
      {open && (
        <motion.div
          className="fixed inset-0 z-50 flex items-center justify-center bg-[var(--c-overlay)] px-4 py-8 backdrop-blur-[2px]"
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
              role="dialog"
              aria-modal="true"
              aria-labelledby={titleId}
              className={`flex max-h-[85vh] w-full flex-col overflow-hidden rounded-2xl border-gray-200 bg-raised p-0 shadow-pop ${sizeClasses[frozen.size]}`}
              onClick={(e) => e.stopPropagation()}
            >
              <div className="flex shrink-0 items-center justify-between gap-4 border-b border-gray-200 px-5 py-3.5">
                <div id={titleId} className="min-w-0 text-[15px] font-semibold text-gray-900">
                  {frozen.title}
                </div>
                <button
                  type="button"
                  aria-label="Cerrar"
                  className="-mr-1.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-gray-500 hover:bg-gray-100 hover:text-gray-800"
                  onClick={onClose}
                >
                  <X size={18} />
                </button>
              </div>
              <div className="overflow-y-auto px-5 py-4">{frozen.children}</div>
              {frozen.footer && <div className="shrink-0 border-t border-gray-200 bg-gray-50/60 px-5 py-3">{frozen.footer}</div>}
            </Card>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  )
}
