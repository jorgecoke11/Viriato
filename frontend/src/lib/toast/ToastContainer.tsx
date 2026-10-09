import { AnimatePresence, motion } from 'framer-motion'
import { CheckCircle2, Info, X, XCircle } from 'lucide-react'
import { slideInRightVariants } from '../motion/variants'
import type { Toast } from './types'

// Colour never carries the meaning alone: each kind has its own icon.
const kinds: Record<Toast['type'], { icon: typeof Info; box: string; iconColor: string }> = {
  success: { icon: CheckCircle2, box: 'border-green-200 bg-green-50 text-green-800', iconColor: 'text-green-600' },
  error: { icon: XCircle, box: 'border-red-200 bg-red-50 text-red-800', iconColor: 'text-red-600' },
  info: { icon: Info, box: 'border-gray-200 bg-raised text-gray-800', iconColor: 'text-indigo-500' },
}

export function ToastContainer({ toasts, onDismiss }: { toasts: Toast[]; onDismiss: (id: string) => void }) {
  // No early-return-when-empty here: the last toast dismissing is exactly the moment `toasts`
  // becomes `[]`, and this container must stay mounted for AnimatePresence to play its exit.
  return (
    <div className="fixed right-4 bottom-4 z-[100] flex w-[calc(100vw-2rem)] max-w-sm flex-col gap-2">
      <AnimatePresence>
        {toasts.map((toast) => {
          const { icon: Icon, box, iconColor } = kinds[toast.type]
          return (
            <motion.div
              key={toast.id}
              layout
              role="alert"
              variants={slideInRightVariants}
              initial="initial"
              animate="animate"
              exit="exit"
              className={`flex items-start gap-3 rounded-xl border px-4 py-3 text-sm shadow-pop ${box}`}
            >
              <Icon size={18} className={`mt-px shrink-0 ${iconColor}`} aria-hidden="true" />
              <span className="min-w-0 flex-1 break-words">{toast.message}</span>
              <button
                type="button"
                className="-m-1 flex h-7 w-7 shrink-0 items-center justify-center rounded-md text-current opacity-60 hover:opacity-100"
                onClick={() => onDismiss(toast.id)}
                aria-label="Cerrar"
              >
                <X size={15} />
              </button>
            </motion.div>
          )
        })}
      </AnimatePresence>
    </div>
  )
}
